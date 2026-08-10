using System.Text.Json;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Data.Seed;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Pages.Admin;

public sealed class CalendarModel(
    AppDbContext db,
    RoomService roomService,
    BookingService bookingService,
    PropertyAccessService propertyAccess) : PageModel
{
    public Guid PropertyId { get; private set; } = DbSeeder.DeLongPropertyId;
    public string PageDataJson { get; private set; } = "{}";

    public async Task<IActionResult> OnGetAsync(DateOnly? from, CancellationToken cancellationToken)
    {
        if (!await propertyAccess.CanAccessAsync(User, PropertyId, cancellationToken))
        {
            return Forbid();
        }

        var property = await db.Properties
            .AsNoTracking()
            .Where(x => x.Id == PropertyId && x.IsActive)
            .Select(x => new { x.Name, x.TimeZoneId })
            .SingleOrDefaultAsync(cancellationToken);
        if (property is null) return NotFound();

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var startDate = from ?? todayLocal;
        var endDateExclusive = startDate.AddDays(7);

        var startLocal = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var endLocal = DateTime.SpecifyKind(endDateExclusive.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);

        var rooms = await roomService.GetAllAsync(PropertyId, cancellationToken);
        var bookings = await bookingService.GetAllAsync(
            PropertyId,
            new DateTimeOffset(startUtc, TimeSpan.Zero),
            new DateTimeOffset(endUtc, TimeSpan.Zero),
            cancellationToken);

        PageDataJson = JsonSerializer.Serialize(
            new
            {
                propertyId = PropertyId,
                propertyName = property.Name,
                timeZoneId = property.TimeZoneId,
                utcOffset = "+07:00",
                startDate = startDate.ToString("yyyy-MM-dd"),
                today = todayLocal.ToString("yyyy-MM-dd"),
                rooms,
                bookings
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return Page();
    }
}
