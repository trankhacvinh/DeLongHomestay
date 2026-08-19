using System.Text.Json;
using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Operations;

public sealed record PublicAvailabilityRealtimeEvent(
    string Type,
    Guid RoomId,
    DateTime OccurredAtUtc);

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
            [FromQuery] Guid? roomId,
            [FromQuery] string? room,
            [FromQuery] string from,
            [FromQuery] int? days,
            [FromQuery] string? siteSlug,
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

            var resolved = await ResolvePublicRoomAsync(roomId, room, siteSlug, db, resolver, cancellationToken);
            if (resolved is null)
            {
                if ((!roomId.HasValue || roomId.Value == Guid.Empty) && string.IsNullOrWhiteSpace(room))
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["room"] = ["Phòng không hợp lệ."]
                    });
                return Results.NotFound();
            }

            var service = new AvailabilityIntervalService(db, resolver, storagePaths);
            var result = await service.GetPublicAsync(
                siteSlug,
                resolved.Value.RoomId,
                startDate,
                Math.Clamp(days ?? 10, 1, 14),
                cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).AllowAnonymous().WithTags("Public Availability");

        app.MapGet("/api/public/room-availability/stream", async (
            HttpContext context,
            [FromQuery] Guid? roomId,
            [FromQuery] string? room,
            [FromQuery] string? siteSlug,
            AppDbContext db,
            PublicPropertyResolver resolver,
            CancellationToken cancellationToken) =>
        {
            var resolved = await ResolvePublicRoomAsync(roomId, room, siteSlug, db, resolver, cancellationToken);
            if (resolved is null)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            await PublicAvailabilityStreamAsync(
                context,
                resolved.Value.PropertyId,
                resolved.Value.RoomId,
                cancellationToken);
        }).AllowAnonymous().WithTags("Public Availability");

        return app;
    }

    private static async Task<(Guid PropertyId, Guid RoomId)?> ResolvePublicRoomAsync(
        Guid? roomId,
        string? room,
        string? siteSlug,
        AppDbContext db,
        PublicPropertyResolver resolver,
        CancellationToken cancellationToken)
    {
        var property = await resolver.ResolveAsync(siteSlug, cancellationToken);
        if (property is null) return null;

        var query = db.Rooms.AsNoTracking()
            .Where(x => x.PropertyId == property.Id && x.IsActive && x.IsPublished);

        Guid? resolvedRoomId = null;
        if (roomId.HasValue && roomId.Value != Guid.Empty)
        {
            resolvedRoomId = await query
                .Where(x => x.Id == roomId.Value)
                .Select(x => (Guid?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(room))
        {
            var key = room.Trim();
            resolvedRoomId = await query
                .Where(x => x.Code == key || x.Slug == key)
                .Select(x => (Guid?)x.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return resolvedRoomId.HasValue ? (property.Id, resolvedRoomId.Value) : null;
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

    private static async Task PublicAvailabilityStreamAsync(
        HttpContext context,
        Guid propertyId,
        Guid roomId,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache, no-store";
        context.Response.Headers.Append("X-Accel-Buffering", "no");
        await context.Response.WriteAsync("retry: 3000\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);

        using var subscription = OperationsRealtimeBroker.Shared.Subscribe(propertyId);
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        while (!cancellationToken.IsCancellationRequested)
        {
            var waitForEvent = subscription.Reader.WaitToReadAsync(cancellationToken).AsTask();
            var heartbeat = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
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
                if (!evt.Type.StartsWith("booking.", StringComparison.Ordinal)) continue;
                if (evt.RoomId.HasValue && evt.RoomId.Value != roomId) continue;

                var payload = new PublicAvailabilityRealtimeEvent(evt.Type, roomId, evt.OccurredAtUtc);
                var json = JsonSerializer.Serialize(payload, jsonOptions);
                await context.Response.WriteAsync(
                    $"event: availability\ndata: {json}\n\n",
                    cancellationToken);
            }
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
}