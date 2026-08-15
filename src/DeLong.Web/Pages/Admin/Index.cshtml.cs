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
    string PropertyName,
    string TimeText,
    decimal BalanceAmount);

public sealed record DashboardRoomItem(string Name, string Code, string PropertyName, HousekeepingStatus Status);

public sealed record DashboardRequestItem(
    string Code,
    string CustomerName,
    string CustomerPhone,
    string RoomName,
    string PropertyName,
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
    public string WorkingPropertyName { get; private set; } = string.Empty;
    public string PropertyName { get; private set; } = "De Long Homestay";
    public string Scope { get; private set; } = string.Empty;
    public bool IsAllScope { get; private set; }
    public IReadOnlyList<CurrentPropertyDto> Properties { get; private set; } = [];
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

    public async Task<IActionResult> OnGetAsync(Guid? propertyId, string? scope, CancellationToken cancellationToken)
    {
        var workingProperty = await currentPropertyService.ResolveAsync(User, propertyId, cancellationToken);
        if (workingProperty is null) return Forbid();

        PropertyId = workingProperty.Id;
        WorkingPropertyName = workingProperty.Name;
        Properties = await currentPropertyService.GetAccessibleAsync(User, cancellationToken);

        var housekeepingOnly = User.IsInRole("Housekeeping") &&
            !User.IsInRole("Admin") &&
            !User.IsInRole("Manager") &&
            !User.IsInRole("Staff") &&
            !User.IsInRole("Viewer");
        if (housekeepingOnly)
        {
            return RedirectToPage("/Admin/Housekeeping/Index", new { propertyId = PropertyId });
        }

        IsAllScope = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) && Properties.Count > 1;
        CurrentPropertyDto? selectedProperty = null;
        if (!IsAllScope && Guid.TryParse(scope, out var scopedId))
            selectedProperty = Properties.SingleOrDefault(x => x.Id == scopedId);
        selectedProperty ??= workingProperty;

        Scope = IsAllScope ? "all" : selectedProperty.Id.ToString();
        PropertyName = IsAllScope ? "Tất cả cơ sở" : selectedProperty.Name;
        IReadOnlyList<CurrentPropertyDto> targets = IsAllScope ? Properties : new[] { selectedProperty };

        var arrivals = new List<DashboardBookingItem>();
        var departures = new List<DashboardBookingItem>();
        var dirtyRooms = new List<DashboardRoomItem>();
        var websiteRequests = new List<DashboardRequestItem>();
        var dateLabels = new List<string>();

        CanViewFinance = User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Viewer");

        foreach (var property in targets)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone);
            var today = DateOnly.FromDateTime(nowLocal);
            dateLabels.Add($"{today:dd/MM/yyyy}");

            var startLocal = DateTime.SpecifyKind(today.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var endLocal = startLocal.AddDays(1);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, timeZone);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, timeZone);

            var bookings = await bookingService.GetAllAsync(
                property.Id,
                new DateTimeOffset(startUtc, TimeSpan.Zero),
                new DateTimeOffset(endUtc, TimeSpan.Zero),
                cancellationToken);
            var rooms = await roomService.GetAllAsync(property.Id, cancellationToken);
            var requests = await requestInboxService.GetRecentAsync(property.Id, 5, cancellationToken);
            RequestedCount += await requestInboxService.CountAsync(property.Id, cancellationToken);

            var activeBookings = bookings
                .Where(x => x.Status is not BookingStatus.Requested and not BookingStatus.Cancelled and not BookingStatus.NoShow)
                .ToList();
            var propertyArrivals = activeBookings
                .Where(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(x.CheckInUtc, timeZone)) == today)
                .OrderBy(x => x.CheckInUtc)
                .ToList();
            var propertyDepartures = activeBookings
                .Where(x => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(x.CheckOutUtc, timeZone)) == today)
                .OrderBy(x => x.CheckOutUtc)
                .ToList();

            ArrivalsCount += propertyArrivals.Count;
            DeparturesCount += propertyDepartures.Count;
            OccupiedCount += activeBookings.Count(x => x.Status == BookingStatus.CheckedIn);
            DirtyCount += rooms.Count(x => x.HousekeepingStatus == HousekeepingStatus.Dirty);

            arrivals.AddRange(propertyArrivals.Select(x => ToItem(x, property.Name, x.CheckInUtc, timeZone)));
            departures.AddRange(propertyDepartures.Select(x => ToItem(x, property.Name, x.CheckOutUtc, timeZone)));
            dirtyRooms.AddRange(rooms
                .Where(x => x.HousekeepingStatus != HousekeepingStatus.Clean)
                .Select(x => new DashboardRoomItem(x.Name, x.Code, property.Name, x.HousekeepingStatus)));
            websiteRequests.AddRange(requests.Select(x =>
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(x.CheckInUtc, timeZone);
                return new DashboardRequestItem(
                    x.Code,
                    x.CustomerName,
                    x.CustomerPhone,
                    x.RoomName,
                    property.Name,
                    local.ToString("dd/MM · HH:mm"),
                    x.TotalAmount);
            }));

            if (CanViewFinance)
            {
                var snapshot = await financeService.GetSnapshotAsync(property.Id, startUtc, endUtc, cancellationToken);
                ReceiptsToday += snapshot.Summary.NetReceipts;
                Outstanding += snapshot.Summary.Outstanding;
            }
        }

        TodayLabel = IsAllScope
            ? (dateLabels.Distinct().Count() == 1 ? dateLabels[0] : "Theo ngày địa phương của từng cơ sở")
            : $"{GetWeekday(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(selectedProperty.TimeZoneId)).DayOfWeek)}, {dateLabels[0]}";

        Arrivals = arrivals.OrderBy(x => x.TimeText).Take(8).ToList();
        Departures = departures.OrderBy(x => x.TimeText).Take(8).ToList();
        DirtyRooms = dirtyRooms
            .OrderByDescending(x => x.Status == HousekeepingStatus.Dirty)
            .ThenBy(x => x.PropertyName)
            .ThenBy(x => x.Name)
            .Take(8)
            .ToList();
        WebsiteRequests = websiteRequests.Take(8).ToList();

        return Page();
    }

    private static DashboardBookingItem ToItem(
        BookingDto booking,
        string propertyName,
        DateTime timeUtc,
        TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(timeUtc, timeZone);
        return new DashboardBookingItem(
            booking.Code,
            booking.CustomerName,
            booking.RoomName,
            propertyName,
            local.ToString("HH:mm"),
            booking.BalanceAmount);
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
