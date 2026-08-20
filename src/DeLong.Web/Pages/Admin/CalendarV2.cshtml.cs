using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin;

[Authorize(Policy = "ViewOperations")]
public sealed class CalendarV2Model(
    RoomService roomService,
    BookingService bookingService,
    CurrentPropertyService currentPropertyService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(DateOnly? from, Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();
        PropertyId = property.Id;

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var startDate = from ?? todayLocal;
        var endDateExclusive = startDate.AddDays(7);

        var startLocal = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(endDateExclusive.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);
        var offset = timeZone.GetUtcOffset(startLocal);
        var offsetText = $"{(offset < TimeSpan.Zero ? "-" : "+")}{Math.Abs(offset.Hours):00}:{Math.Abs(offset.Minutes):00}";

        var rooms = (await roomService.GetAllAsync(PropertyId, cancellationToken))
            .Select(room => room with
            {
                Rates = room.Rates.Where(rate => rate.Type != RoomRateType.Nightly).ToList()
            })
            .ToList();

        var bookings = (await bookingService.GetAllAsync(
                PropertyId,
                new DateTimeOffset(startUtc, TimeSpan.Zero),
                new DateTimeOffset(endUtc, TimeSpan.Zero),
                cancellationToken))
            .Where(booking => booking.Status is BookingStatus.Requested or BookingStatus.Held or BookingStatus.Confirmed or BookingStatus.CheckedIn)
            .ToList();

        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = property.Name,
                timeZoneId = property.TimeZoneId,
                utcOffset = offsetText,
                startDate = startDate.ToString("yyyy-MM-dd"),
                today = todayLocal.ToString("yyyy-MM-dd"),
                rooms,
                bookings
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return Page();
    }
}
