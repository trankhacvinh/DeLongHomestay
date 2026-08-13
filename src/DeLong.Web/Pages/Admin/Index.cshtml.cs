using DeLong.Web.Common.Security;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Finance;
using DeLong.Web.Features.PublicBooking;
using DeLong.Web.Features.Rooms;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DeLong.Web.Pages.Admin;

public sealed record DashboardBookingItem(
    string Code,
    string CustomerName,
    string RoomName,
    string TimeText,
    decimal BalanceAmount);

public sealed record DashboardRoomItem(string Name, string Code, HousekeepingStatus Status);

public sealed record DashboardRequestItem(
    string Code,
    string CustomerName,
    string CustomerPhone,
    string RoomName,
    string DateTimeText,
    decimal TotalAmount);

public sealed class IndexModel(
    CurrentPropertyService currentPropertyService,
    BookingService bookingService,
    RoomService roomService,
    FinanceService financeService,
    PublicRequestInboxService requestInboxService) : PageModel
{
    public Guid PropertyId { get; private set; }
    public string PropertyName { get; private set; } = "De Long Homestay";
    public string TodayLabel { get; private set; } = string.Empty;
    public int ArrivalsCount { get; private set; }
    public int DeparturesCount { get; private set; }
    public int OccupiedCount { get; private set; }
    public int DirtyCount { get; private set; }
    public int RequestedCount { get; private set; }
    public bool CanViewFinance { get; private set; }
    public decimal ReceiptsToday { get; private set; }
    public decimal Outstanding { get; private set; }
    public IReadOnlyList<DashboardBookingItem> Arrivals { get; private set; } = [];
    public IReadOnlyList<DashboardBookingItem> Departures { get; private set; } = [];
    public IReadOnlyList<DashboardRoomItem> DirtyRooms { get; private set; } = [];
    public IReadOnlyList<DashboardRequestItem> WebsiteRequests { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, CancellationToken cancellationToken)
    {
        var property = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (property is null) return Forbid();

        PropertyId = property.Id;
        PropertyName = property.Name;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
        var today = DateOnly.FromDateTime(nowLocal);
        TodayLabel = $"{GetWeekday(nowLocal.DayOfWeek)}, {today:dd/MM/yyyy}";

        var startLocal = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
        var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);

        var bookings = await bookingService.GetAllAsync(
            PropertyId,
            new DateTimeOffset(startUtc, TimeSpan.Zero),
            new DateTimeOffset(endUtc, TimeSpan.Zero),
            cancellationToken);
        var rooms = await roomService.GetAllAsync(PropertyId, cancellationToken);
        var requests = await requestInboxService.GetRecentAsync(PropertyId, 5, cancellationToken);
        RequestedCount = await requestInboxService.CountAsync(PropertyId, cancellationToken);

        var activeBookings = bookings.Where(x => x.Status is not BookingStatus.Requested and not BookingStatus.Cancelled and not BookingStatus.NoShow).ToList();
        var arrivals = activeBookings.Where(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(x.CheckInUtc, timeZone)) == today).OrderBy(x => x.CheckInUtc).ToList();
        var departures = activeBookings.Where(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(x.CheckOutUtc, timeZone)) == today).OrderBy(x => x.CheckOutUtc).ToList();

        ArrivalsCount = arrivals.Count;
        DeparturesCount = departures.Count;
        OccupiedCount = activeBookings.Count(x => x.Status == BookingStatus.CheckedIn);
        DirtyCount = rooms.Count(x => x.HousekeepingStatus == HousekeepingStatus.Dirty);

        Arrivals = arrivals.Take(6).Select(x => ToItem(x, x.CheckInUtc, timeZone)).ToList();
        Departures = departures.Take(6).Select(x => ToItem(x, x.CheckOutUtc, timeZone)).ToList();
        DirtyRooms = rooms.Where(x => x.HousekeepingStatus != HousekeepingStatus.Clean)
            .OrderByDescending(x => x.HousekeepingStatus == HousekeepingStatus.Dirty)
            .Select(x => new DashboardRoomItem(x.Name, x.Code, x.HousekeepingStatus))
            .Take(6)
            .ToList();
        WebsiteRequests = requests.Select(x =>
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(x.CheckInUtc, timeZone);
            return new DashboardRequestItem(
                x.Code,
                x.CustomerName,
                x.CustomerPhone,
                x.RoomName,
                local.ToString("dd/MM · HH:mm"),
                x.TotalAmount);
        }).ToList();

        CanViewFinance = User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Viewer");
        if (CanViewFinance)
        {
            var snapshot = await financeService.GetSnapshotAsync(PropertyId, startUtc, endUtc, cancellationToken);
            ReceiptsToday = snapshot.Summary.NetReceipts;
            Outstanding = snapshot.Summary.Outstanding;
        }

        return Page();
    }

    private static DashboardBookingItem ToItem(BookingDto booking, DateTime timeUtc, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, timeZone);
        return new DashboardBookingItem(booking.Code, booking.CustomerName, booking.RoomName, local.ToString("HH:mm"), booking.BalanceAmount);
    }

    private static string GetWeekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        _ => "Chủ Nhật"
    };
}
