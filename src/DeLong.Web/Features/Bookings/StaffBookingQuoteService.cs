using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Pricing;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Bookings;

public sealed record StaffBookingQuoteSlot(
    string Date, Guid RateId, string RateName, DateTime StartUtc, DateTime EndUtc,
    decimal ListPrice, decimal AppliedAmount, decimal SpecialSurchargeAmount, decimal ComboDiscountAmount, string? SpecialDayName);

public sealed record StaffBookingQuote(
    IReadOnlyList<StaffBookingQuoteSlot> Slots, decimal RoomAmount, decimal SpecialSurchargeAmount, decimal Total);

/// <summary>
/// Prices a staff booking the way the guest website does: every time slot ("ca") the stay touches is
/// counted, in the same consecutive order the website allows (next ca of the day, then the first ca of
/// the next day), and the shared <see cref="PricingService"/> applies weekend prices, special days,
/// multi-slot combo discounts and the full-day price. Staff can still overwrite the result in the form.
/// </summary>
public sealed class StaffBookingQuoteService(AppDbContext db, PricingService pricingService)
{
    private const int MaxSlots = 60;

    public sealed record RateWindow(int Index, Guid Id, string Name, TimeOnly Start, TimeOnly End, bool Overnight);

    public async Task<(StaffBookingQuote? Quote, string? Error)> QuoteAsync(
        Guid propertyId, Guid roomId, Guid? rateId, DateTime checkInUtc, DateTime checkOutUtc, CancellationToken ct)
    {
        if (checkOutUtc <= checkInUtc || checkOutUtc - checkInUtc > TimeSpan.FromDays(20)) return (null, "Giờ nhận/trả phòng không hợp lệ.");
        var room = await db.Rooms.AsNoTracking()
            .Where(x => x.Id == roomId && x.PropertyId == propertyId)
            .Select(x => new
            {
                x.FullDayPricingEnabled, x.FullDayPrice, x.UseWeekdayFullDayPriceOnWeekend, x.WeekendFullDayPrice, x.Property.TimeZoneId,
                Rates = x.Rates.Where(r => r.IsActive && r.Type != RoomRateType.Nightly)
                    .OrderBy(r => r.SortOrder).ThenBy(r => r.StartTime).ThenBy(r => r.Name)
                    .Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Type, r.IsOvernight, r.Price, r.UseWeekdayPriceOnWeekend, r.WeekendPrice }).ToList()
            }).SingleOrDefaultAsync(ct);
        if (room is null || room.Rates.Count == 0) return (null, null);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(room.TimeZoneId);
        var windows = room.Rates.Select((r, i) => new RateWindow(i, r.Id, r.Name, r.StartTime, r.EndTime,
            r.Type == RoomRateType.Overnight || r.IsOvernight || r.EndTime <= r.StartTime)).ToList();

        var picked = PickSlots(windows, rateId, checkInUtc, checkOutUtc, zone);
        if (picked.Count == 0) return (null, null);

        var inputs = picked.Select(x =>
        {
            var rate = room.Rates[x.Window.Index];
            return new PricingSelectionInput(x.Date, x.Window.Index, rate.Id, rate.Name, rate.Price, rate.UseWeekdayPriceOnWeekend, rate.WeekendPrice);
        }).ToList();
        var (pricing, error) = await pricingService.CalculateAsync(propertyId, inputs, room.Rates.Count,
            room.FullDayPricingEnabled, room.FullDayPrice, room.UseWeekdayFullDayPriceOnWeekend, room.WeekendFullDayPrice, ct);
        if (error is not null) return (null, error.Message);
        var slots = picked.Select((x, i) =>
        {
            var s = pricing!.Segments[i];
            return new StaffBookingQuoteSlot(x.Date.ToString("yyyy-MM-dd"), x.Window.Id, x.Window.Name, x.StartUtc, x.EndUtc,
                s.ListPrice, s.AppliedAmount, s.SpecialSurchargeAmount, s.ComboDiscountAmount, s.SpecialDayName);
        }).ToList();
        return (new StaffBookingQuote(slots, pricing!.RoomAmount, pricing.SpecialSurchargeAmount, pricing.TotalBeforeVoucher), null);
    }

    public sealed record PickedSlot(DateOnly Date, RateWindow Window, DateTime StartUtc, DateTime EndUtc);

    /// <summary>
    /// The first ca is the one staff picked (or, without one, the ca the check-in falls in). Following
    /// ca are added in website order until the stay's check-out is covered.
    /// </summary>
    public static IReadOnlyList<PickedSlot> PickSlots(
        IReadOnlyList<RateWindow> windows, Guid? rateId, DateTime checkInUtc, DateTime checkOutUtc, TimeZoneInfo zone)
    {
        if (windows.Count == 0) return [];
        var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(checkInUtc, zone));
        var candidates = new[] { localDay.AddDays(-1), localDay }
            .SelectMany(day => windows.Select(w => Resolve(day, w, zone))).ToList();

        PickedSlot? first = null;
        var chosen = rateId.HasValue ? windows.FirstOrDefault(x => x.Id == rateId.Value) : null;
        if (chosen is not null)
        {
            first = candidates.Where(x => x.Window.Id == chosen.Id && x.StartUtc <= checkInUtc && checkInUtc < x.EndUtc)
                        .OrderByDescending(x => x.StartUtc).FirstOrDefault()
                    ?? Resolve(localDay, chosen, zone);
        }
        else
        {
            first = candidates.Where(x => x.StartUtc <= checkInUtc && checkInUtc < x.EndUtc)
                        .OrderByDescending(x => x.StartUtc).FirstOrDefault()
                    ?? candidates.Where(x => x.Date == localDay && x.StartUtc > checkInUtc && x.StartUtc < checkOutUtc)
                        .OrderBy(x => x.StartUtc).FirstOrDefault();
        }
        if (first is null) return [];

        var result = new List<PickedSlot> { first };
        var current = first;
        while (current.EndUtc < checkOutUtc && result.Count < MaxSlots)
        {
            var last = current.Window.Index == windows.Count - 1;
            var next = Resolve(last ? current.Date.AddDays(1) : current.Date, windows[last ? 0 : current.Window.Index + 1], zone);
            if (next.StartUtc >= checkOutUtc) break;
            result.Add(next);
            current = next;
        }
        return result;
    }

    private static PickedSlot Resolve(DateOnly day, RateWindow window, TimeZoneInfo zone)
    {
        var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(day.ToDateTime(window.Start), DateTimeKind.Unspecified), zone);
        var end = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind((window.Overnight ? day.AddDays(1) : day).ToDateTime(window.End), DateTimeKind.Unspecified), zone);
        return new PickedSlot(day, window, start, end);
    }
}
