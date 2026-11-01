using System.Security.Claims;
using DeLong.Web.Common.Operations;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Features.Pricing;
using DeLong.Web.Features.Site;
using Microsoft.AspNetCore.Mvc;

namespace DeLong.Web.Features.Operations;

public sealed record CalendarOverviewPropertyOptionDto(Guid Id, string Code, string Name);

public sealed record CalendarOverviewPropertyDto(
    Guid Id,
    string Code,
    string Name,
    string TimeZoneId,
    BookingCalendarDisplaySettings Colors,
    IReadOnlyList<AdminRoomAvailabilityDto> Rooms);

public sealed record CalendarOverviewDto(
    DateOnly From,
    int Days,
    Guid? SelectedPropertyId,
    bool Truncated,
    IReadOnlyList<CalendarOverviewPropertyOptionDto> AccessibleProperties,
    IReadOnlyList<CalendarOverviewPropertyDto> Properties);

/// <summary>
/// Multi-room, multi-branch calendar overview for the admin calendar (mobile pinch/pan grid and
/// desktop room picker). Visibility is decided only on the server: a user sees the rooms of the
/// branches assigned to them through UserPropertyAccesses, so an Admin or a user assigned to several
/// branches sees all of those branches, and a single-branch account sees only its own rooms.
/// </summary>
public static class CalendarOverviewEndpoints
{
    public const int MaxRoomsPerOverview = 80;

    public static IEndpointRouteBuilder MapCalendarOverviewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admin/calendar/overview", async (
            [FromQuery] string from,
            [FromQuery] int? days,
            [FromQuery] Guid? propertyId,
            ClaimsPrincipal user,
            CurrentPropertyService currentPropertyService,
            AppDbContext db,
            PublicPropertyResolver resolver,
            StoragePaths storagePaths,
            PricingService pricingService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!DateOnly.TryParseExact(from, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var startDate) || startDate.Year is < 1900 or > 9998)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["from"] = ["Ngày bắt đầu không hợp lệ."]
                });

            var accessible = await currentPropertyService.GetAccessibleAsync(user, cancellationToken);
            if (accessible.Count == 0) return Results.Forbid();

            IReadOnlyList<CurrentPropertyDto> selected = accessible;
            if (propertyId.HasValue)
            {
                var match = accessible.SingleOrDefault(x => x.Id == propertyId.Value);
                if (match is null) return Results.Forbid();
                selected = [match];
            }

            var rangeDays = Math.Clamp(days ?? 14, 1, 31);
            var service = new AvailabilityIntervalService(db, resolver, storagePaths, pricingService);
            var settingsStore = new BookingCalendarDisplaySettingsStore(storagePaths);
            var remaining = MaxRoomsPerOverview;
            var truncated = false;
            var properties = new List<CalendarOverviewPropertyDto>(selected.Count);
            foreach (var property in selected)
            {
                if (remaining <= 0) { truncated = true; break; }
                var rooms = await service.GetAdminManyAsync(property.Id, startDate, rangeDays, remaining + 1, cancellationToken);
                if (rooms.Count > remaining) { truncated = true; rooms = rooms.Take(remaining).ToList(); }
                remaining -= rooms.Count;
                var colors = await settingsStore.GetAsync(property.Id, cancellationToken);
                properties.Add(new CalendarOverviewPropertyDto(
                    property.Id, property.Code, property.Name, property.TimeZoneId, colors,
                    rooms.Select(WithoutPhone).ToList()));
            }

            return Results.Ok(new CalendarOverviewDto(
                startDate,
                rangeDays,
                propertyId,
                truncated,
                accessible.Select(x => new CalendarOverviewPropertyOptionDto(x.Id, x.Code, x.Name)).ToList(),
                properties));
        })
        .RequireAuthorization("ViewOperations")
        .WithTags("Operations");

        return app;
    }

    // The overview shows guest names only; phone numbers stay in the booking detail dialog.
    private static AdminRoomAvailabilityDto WithoutPhone(AdminRoomAvailabilityDto room) => room with
    {
        Calendar = room.Calendar.Select(day => day with
        {
            Slots = day.Slots.Select(slot => slot with
            {
                Occupied = slot.Occupied.Select(range => range with { CustomerPhone = "" }).ToList()
            }).ToList()
        }).ToList()
    };
}
