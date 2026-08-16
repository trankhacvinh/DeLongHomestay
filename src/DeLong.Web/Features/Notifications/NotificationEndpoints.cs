using System.Security.Claims;
using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/properties/{propertyId:guid}/notifications")
            .RequireAuthorization("ViewOperations")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Notifications");

        group.MapGet("/", async (
            Guid propertyId,
            int? take,
            ClaimsPrincipal user,
            BookingNotificationService service,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            return userId is null
                ? Results.Unauthorized()
                : Results.Ok(await service.GetFeedAsync(propertyId, userId.Value, take ?? 20, cancellationToken));
        });

        group.MapPost("/{notificationId:guid}/read", async (
            Guid propertyId,
            Guid notificationId,
            ClaimsPrincipal user,
            BookingNotificationService service,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            return await service.MarkReadAsync(propertyId, notificationId, userId.Value, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/read-all", async (
            Guid propertyId,
            ClaimsPrincipal user,
            BookingNotificationService service,
            CancellationToken cancellationToken) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();
            var count = await service.MarkAllReadAsync(propertyId, userId.Value, cancellationToken);
            return Results.Ok(new { count });
        }).AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapGet("/stream", StreamAsync);

        group.MapGet("/settings", async (
            Guid propertyId,
            NotificationSettingsService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(propertyId, cancellationToken)))
            .RequireAuthorization("ManageNotifications");

        group.MapPut("/settings", async (
            Guid propertyId,
            UpdateNotificationSettingsRequest request,
            NotificationSettingsService service,
            CancellationToken cancellationToken) =>
        {
            var (settings, error) = await service.SaveAsync(propertyId, request, cancellationToken);
            return error is null ? Results.Ok(settings) : ToProblem(error);
        })
            .RequireAuthorization("ManageNotifications")
            .AddEndpointFilter<ApiAntiforgeryFilter>();

        group.MapPost("/settings/test-email", async (
            Guid propertyId,
            NotificationSettingsService settingsService,
            NotificationEmailSender sender,
            AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var (profile, recipients, error) = await settingsService.GetDeliveryProfileAsync(propertyId, false, cancellationToken);
            if (error is not null || profile is null) return ToProblem(error ?? new("smtp_not_configured", "SMTP chưa được cấu hình."));

            var settings = await db.Set<PropertyNotificationSettings>().SingleAsync(x => x.PropertyId == propertyId, cancellationToken);
            try
            {
                await sender.SendAsync(
                    profile,
                    recipients,
                    "De Long Homestay · Email thông báo thử",
                    "Đây là email thử từ cấu hình thông báo De Long Homestay. Nếu bạn nhận được email này, SMTP đang hoạt động.",
                    cancellationToken);
                settings.LastEmailSentAtUtc = DateTime.UtcNow;
                settings.LastEmailError = null;
                settings.LastEmailErrorAtUtc = null;
                await db.SaveChangesAsync(cancellationToken);
                return Results.Ok(new { sent = true, recipients });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                settings.LastEmailError = ex.Message.Length <= 2000 ? ex.Message : ex.Message[..2000];
                settings.LastEmailErrorAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                return Results.Problem(
                    type: "https://delong.local/problems/smtp_test_failed",
                    title: "Không gửi được email thử",
                    detail: "Không thể kết nối hoặc xác thực SMTP. Kiểm tra host, port, SSL, username/password và email người gửi.",
                    statusCode: StatusCodes.Status502BadGateway,
                    extensions: new Dictionary<string, object?> { ["code"] = "smtp_test_failed" });
            }
        })
            .RequireAuthorization("ManageNotifications")
            .AddEndpointFilter<ApiAntiforgeryFilter>();

        return app;
    }

    private static async Task StreamAsync(
        HttpContext context,
        Guid propertyId,
        NotificationRealtimeBroker broker,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers.Append("X-Accel-Buffering", "no");
        await context.Response.WriteAsync("retry: 3000\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        using var subscription = broker.Subscribe(propertyId);
        while (!cancellationToken.IsCancellationRequested)
        {
            var waitForEvent = subscription.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var heartbeat = Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            var completed = await Task.WhenAny(waitForEvent, heartbeat);
            if (completed == heartbeat)
            {
                await context.Response.WriteAsync(": keep-alive\n\n", cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
                continue;
            }

            if (!await waitForEvent) break;
            while (subscription.Reader.TryRead(out var evt))
            {
                var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                await context.Response.WriteAsync($"id: {evt.NotificationId}\nevent: notification\ndata: {json}\n\n", cancellationToken);
            }
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var userId) ? userId : null;
    }

    private static IResult ToProblem(NotificationOperationError error)
    {
        var status = error.Code == "property_not_found" ? 404 : 400;
        return Results.Problem(
            type: $"https://delong.local/problems/{error.Code}",
            title: "Không thể lưu cấu hình thông báo",
            detail: error.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
