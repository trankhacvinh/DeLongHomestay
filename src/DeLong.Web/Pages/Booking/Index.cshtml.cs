using System.Text.Json;
using DeLong.Web.Features.PublicBooking;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Booking;

public sealed class IndexModel(PublicBookingService publicBookingService) : PageModel
{
    public string PageDataJson { get; private set; } = "{}";

    public async Task OnGetAsync(string? date, string? room, Guid? rate, CancellationToken cancellationToken)
    {
        var catalog = await publicBookingService.GetCatalogAsync(null, cancellationToken);
        if (catalog is null) return;

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(catalog.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var selectedDate = DateOnly.TryParse(date, out var parsedDate) && parsedDate >= today ? parsedDate : today;
        var selectedRoom = catalog.Rooms.FirstOrDefault(x => string.Equals(x.Code, room, StringComparison.OrdinalIgnoreCase));
        var selectedRate = rate.HasValue
            ? catalog.Rooms.SelectMany(x => x.Rates).FirstOrDefault(x => x.Id == rate.Value)
            : null;

        PageDataJson = JsonSerializer.Serialize(new
        {
            propertyName = catalog.PropertyName,
            timeZoneId = catalog.TimeZoneId,
            date = selectedDate.ToString("yyyy-MM-dd"),
            rooms = catalog.Rooms,
            initialRoomId = selectedRoom?.Id,
            initialRateId = selectedRate?.Id
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
}
