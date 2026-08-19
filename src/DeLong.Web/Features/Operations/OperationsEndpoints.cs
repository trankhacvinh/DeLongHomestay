using System.Text.Json;
using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;

namespace DeLong.Web.Features.Operations;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        // The stream itself is operational metadata only and is shared by Calendar/Bookings/Housekeeping.
        // AdminArea includes the Housekeeping role; the availability endpoint below remains ViewOperations-only.
        var admin = app.MapGroup("/api/admin/properties/{propertyId:guid}/operations")
            .RequireAuthorization("AdminArea")
            .AddEndpointFilter<PropertyAccessFilter>()
            .WithTags("Operations");

        admin.MapGet("/stream", StreamAsync);

        admin.MapGet("/availability/rooms/{roomId:guid}", async (
            Guid propertyId,
            Guid roomId,
            [FromQuery] string from,
            [FromQuery] int? days,
            AppDbContext db,
            PublicPropertyResolver resolver,
            StoragePaths storagePaths,
            CancellationToken cancellationToken) =>
        {
            if (!DateOnly.TryParse(from, out var startDate))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["from"] = ["Ngày bắt đầu không hợp lệ."]
                });

            var service = new AvailabilityIntervalService(db, resolver, storagePaths);
            var result = await service.GetAdminAsync(
                propertyId,
                roomId,
                startDate,
                Math.Clamp(days ?? 10, 1, 31),
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization("ViewOperations");

        app.MapGet("/api/public/room-availability", async (
            [FromQuery] Guid roomId,
            [FromQuery] string from,
            [FromQuery] int? days,
            [FromQuery] string? siteSlug,
            AppDbContext db,
            PublicPropertyResolver resolver,
            StoragePaths storagePaths,
            CancellationToken cancellationToken) =>
        {
            if (roomId == Guid.Empty)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["roomId"] = ["Phòng không hợp lệ."]
                });
            if (!DateOnly.TryParse(from, out var startDate))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["from"] = ["Ngày bắt đầu không hợp lệ."]
                });

            var service = new AvailabilityIntervalService(db, resolver, storagePaths);
            var result = await service.GetPublicAsync(
                siteSlug,
                roomId,
                startDate,
                Math.Clamp(days ?? 10, 1, 14),
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).AllowAnonymous().WithTags("Public Availability");

        return app;
    }

    private static async Task StreamAsync(
        HttpContext context,
        Guid propertyId,
        IServiceScopeFactory scopeFactory,
        StoragePaths storagePaths,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers.Append("X-Accel-Buffering", "no");
        await context.Response.WriteAsync("retry: 2000\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        using var subscription = OperationsRealtimeBroker.Shared.Subscribe(propertyId);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var nextHoldSweepAtUtc = DateTime.UtcNow;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (DateTime.UtcNow >= nextHoldSweepAtUtc)
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await new PublicBookingHoldStore(storagePaths).ReleaseExpiredAsync(db, propertyId, cancellationToken);
                nextHoldSweepAtUtc = DateTime.UtcNow.AddSeconds(5);
            }

            var waitForEvent = subscription.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var heartbeat = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
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
                var json = JsonSerializer.Serialize(evt, jsonOptions);
                await context.Response.WriteAsync(
                    $"id: {evt.EventId}\nevent: operations\ndata: {json}\n\n",
                    cancellationToken);
            }
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
}
