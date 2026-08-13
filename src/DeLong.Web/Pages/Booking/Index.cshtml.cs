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
        PublicRateDto? selectedRate = null;
        if (rate.HasValue)
        {
            var rateRoom = catalog.Rooms.FirstOrDefault(x => x.Rates.Any(r => r.Id == rate.Value));
            if (rateRoom is not null && (selectedRoom is null || selectedRoom.Id == rateRoom.Id))
            {
                selectedRoom ??= rateRoom;
                selectedRate = rateRoom.Rates.First(r => r.Id == rate.Value);
            }
        }

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
