using System.Globalization;
using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Bookings;
using DeLong.Web.Features.Customers;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.PublicBooking;

public sealed class PublicBookingService(AppDbContext db, BookingService bookingService)
{
    private const string PublicPropertyCode = "DELONG";
    private static readonly BookingStatus[] LockingStatuses = [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<PublicCatalogDto?> GetCatalogAsync(DateOnly? availabilityDate = null, CancellationToken cancellationToken = default)
    {
        var property = await db.Properties.AsNoTracking().Where(x => x.Code == PublicPropertyCode && x.IsActive).Select(x => new { x.Id, x.Name, x.TimeZoneId }).SingleOrDefaultAsync(cancellationToken);
        if (property is null) return null;
        HashSet<(Guid RoomId, Guid RateId)> unavailable = availabilityDate.HasValue ? await GetUnavailableRateKeysAsync(property.Id, property.TimeZoneId, availabilityDate.Value, cancellationToken) : [];
        var rooms = await db.Rooms.AsNoTracking().Where(x => x.PropertyId == property.Id && x.IsActive && x.IsPublished).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Capacity, Rates = x.Rates.Where(r => r.IsActive).OrderBy(r => r.SortOrder).Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Type, r.IsOvernight, r.Price }).ToList() }).ToListAsync(cancellationToken);
        var roomDtos = rooms.Select(room =>
        {
            var rates = room.Rates.Select(rate => new PublicRateDto(rate.Id, rate.Name, rate.StartTime.ToString("HH:mm"), rate.EndTime.ToString("HH:mm"), rate.Type, rate.IsOvernight, rate.Price, !availabilityDate.HasValue || !unavailable.Contains((room.Id, rate.Id)))).ToList();
            var publicPrices = rates.Where(r => r.Price > 0).Select(r => r.Price).ToList();
            return new PublicRoomDto(room.Id, room.Code, room.Name, room.Capacity, HasBathtub(room.Code), publicPrices.Count == 0 ? 0 : publicPrices.Min(), rates);
        }).ToList();
        return new PublicCatalogDto(property.Id, property.Name, property.TimeZoneId, roomDtos);
    }

    public async Task<PublicRoomDto?> GetRoomAsync(string code, CancellationToken cancellationToken = default) => (await GetCatalogAsync(null, cancellationToken))?.Rooms.SingleOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
    public async Task<PublicAvailabilityDto?> GetAvailabilityAsync(DateOnly date, CancellationToken cancellationToken = default) { var catalog = await GetCatalogAsync(date, cancellationToken); return catalog is null ? null : new(date.ToString("yyyy-MM-dd"), catalog.Rooms); }

    public async Task<(PublicStayAvailabilityDto? Availability, PublicBookingError? Error)> GetStayAvailabilityAsync(DateOnly checkInDate, DateOnly checkOutDate, CancellationToken cancellationToken = default)
    {
        var property = await db.Properties.AsNoTracking().Where(x => x.Code == PublicPropertyCode && x.IsActive).Select(x => new { x.Id, x.TimeZoneId }).SingleOrDefaultAsync(cancellationToken);
        if (property is null) return (null, new("property_not_found", "Cơ sở hiện không khả dụng."));
        var validation = ValidateStayDates(checkInDate, checkOutDate, property.TimeZoneId); if (validation is not null) return (null, validation);
        var nights = checkOutDate.DayNumber - checkInDate.DayNumber; var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var rates = await db.RoomRates.AsNoTracking().Where(r => r.Room.PropertyId == property.Id && r.Room.IsActive && r.Room.IsPublished && r.IsActive && r.Type == RoomRateType.Nightly && r.Price > 0)
            .OrderBy(r => r.Room.SortOrder).ThenBy(r => r.SortOrder).Select(r => new { r.Id, r.Name, r.StartTime, r.EndTime, r.Price, RoomId = r.Room.Id, RoomCode = r.Room.Code, RoomName = r.Room.Name, r.Room.Capacity }).ToListAsync(cancellationToken);
        var roomRates = rates.GroupBy(r => r.RoomId).Select(g => g.First()).ToList();
        var results = new List<PublicStayRoomDto>();
        foreach (var rate in roomRates)
        {
            var (checkInUtc, checkOutUtc) = ToUtcStayRange(checkInDate, checkOutDate, rate.StartTime, rate.EndTime, timeZone);
            var conflict = await bookingService.HasConflictAsync(property.Id, rate.RoomId, checkInUtc, checkOutUtc, null, cancellationToken);
            var dto = new PublicRateDto(rate.Id, rate.Name, rate.StartTime.ToString("HH:mm"), rate.EndTime.ToString("HH:mm"), RoomRateType.Nightly, false, rate.Price, !conflict);
            results.Add(new PublicStayRoomDto(rate.RoomId, rate.RoomCode, rate.RoomName, rate.Capacity, HasBathtub(rate.RoomCode), dto, nights, rate.Price * nights, !conflict));
        }
        return (new PublicStayAvailabilityDto(checkInDate.ToString("yyyy-MM-dd"), checkOutDate.ToString("yyyy-MM-dd"), nights, results), null);
    }

    public async Task<(PublicBookingResult? Result, PublicBookingError? Error)> CreateRequestAsync(PublicBookingRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Website)) return (null, new("spam", "Không thể gửi yêu cầu."));
        return request.Type == BookingType.MultiDay ? await CreateMultiDayRequestAsync(request, cancellationToken) : await CreateTimeSlotRequestAsync(request, cancellationToken);
    }

    private async Task<(PublicBookingResult?, PublicBookingError?)> CreateTimeSlotRequestAsync(PublicBookingRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(request.StayDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var stayDate)) return (null, new("validation", "Ngày đặt phòng không hợp lệ."));
        var context = await GetRequestContextAsync(request.CustomerName, request.CustomerPhone, ct); if (context.Error is not null) return (null, context.Error);
        if (ValidateDateWindow(stayDate, context.TodayLocal) is { } dateError) return (null, dateError);
        var rate = await db.RoomRates.AsNoTracking().Where(x => x.Id == request.RateId && x.RoomId == request.RoomId && x.IsActive && x.Type != RoomRateType.Nightly && x.Room.IsActive && x.Room.IsPublished && x.Room.PropertyId == context.PropertyId)
            .Select(x => new { x.Id, x.Name, x.StartTime, x.EndTime, x.Type, x.Price, RoomId = x.Room.Id, RoomName = x.Room.Name }).SingleOrDefaultAsync(ct);
        if (rate is null) return (null, new("rate_not_found", "Khung giờ hoặc phòng không còn khả dụng."));
        var (checkInUtc, checkOutUtc) = ToUtcTimeSlotRange(stayDate, rate.StartTime, rate.EndTime, rate.Type == RoomRateType.Overnight, context.TimeZone);
        if (await bookingService.HasConflictAsync(context.PropertyId, rate.RoomId, checkInUtc, checkOutUtc, null, ct)) return (null, new("booking_conflict", "Phòng vừa được giữ hoặc xác nhận trong khung giờ này. Vui lòng chọn khung khác."));
        var (booking, error) = await bookingService.CreateAsync(context.PropertyId, new CreateBookingRequest { RoomId = rate.RoomId, CustomerName = context.Name, CustomerPhone = context.Phone, Type = BookingType.TimeSlot, RoomRateId = rate.Id, RateName = rate.Name, UnitPrice = rate.Price, CheckIn = new DateTimeOffset(checkInUtc, TimeSpan.Zero), CheckOut = new DateTimeOffset(checkOutUtc, TimeSpan.Zero), Status = BookingStatus.Requested, RoomAmount = rate.Price, Source = "Website", Note = Clean(request.Note) }, null, ct);
        return booking is null ? (null, new(error?.Code ?? "booking_failed", error?.Message ?? "Không thể tạo yêu cầu đặt phòng.")) : (new PublicBookingResult(booking.Id, booking.Code, booking.Type, rate.RoomName, rate.Name, null, booking.CheckInUtc, booking.CheckOutUtc, booking.TotalAmount), null);
    }

    private async Task<(PublicBookingResult?, PublicBookingError?)> CreateMultiDayRequestAsync(PublicBookingRequest request, CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(request.CheckInDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var checkInDate) || !DateOnly.TryParseExact(request.CheckOutDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var checkOutDate)) return (null, new("validation", "Ngày nhận hoặc ngày trả không hợp lệ."));
        var context = await GetRequestContextAsync(request.CustomerName, request.CustomerPhone, ct); if (context.Error is not null) return (null, context.Error);
        var dateError = ValidateStayDates(checkInDate, checkOutDate, context.TimeZone.Id); if (dateError is not null) return (null, dateError);
        var rate = await db.RoomRates.AsNoTracking().Where(x => x.Id == request.RateId && x.RoomId == request.RoomId && x.IsActive && x.Type == RoomRateType.Nightly && x.Price > 0 && x.Room.IsActive && x.Room.IsPublished && x.Room.PropertyId == context.PropertyId)
            .Select(x => new { x.Id, x.Name, x.StartTime, x.EndTime, x.Price, RoomId = x.Room.Id, RoomName = x.Room.Name }).SingleOrDefaultAsync(ct);
        if (rate is null) return (null, new("rate_not_found", "Phòng chưa có giá lưu trú theo đêm hoặc giá đã ngừng áp dụng."));
        var nights = checkOutDate.DayNumber - checkInDate.DayNumber;
        var (checkInUtc, checkOutUtc) = ToUtcStayRange(checkInDate, checkOutDate, rate.StartTime, rate.EndTime, context.TimeZone);
        if (await bookingService.HasConflictAsync(context.PropertyId, rate.RoomId, checkInUtc, checkOutUtc, null, ct)) return (null, new("booking_conflict", "Phòng đã có lượt đặt giao với khoảng lưu trú này. Vui lòng chọn ngày hoặc phòng khác."));
        var amount = rate.Price * nights;
        var (booking, error) = await bookingService.CreateAsync(context.PropertyId, new CreateBookingRequest { RoomId = rate.RoomId, CustomerName = context.Name, CustomerPhone = context.Phone, Type = BookingType.MultiDay, RoomRateId = rate.Id, RateName = rate.Name, UnitPrice = rate.Price, NightCount = nights, CheckIn = new DateTimeOffset(checkInUtc, TimeSpan.Zero), CheckOut = new DateTimeOffset(checkOutUtc, TimeSpan.Zero), Status = BookingStatus.Requested, RoomAmount = amount, Source = "Website", Note = Clean(request.Note) }, null, ct);
        return booking is null ? (null, new(error?.Code ?? "booking_failed", error?.Message ?? "Không thể tạo yêu cầu lưu trú.")) : (new PublicBookingResult(booking.Id, booking.Code, booking.Type, rate.RoomName, rate.Name, nights, booking.CheckInUtc, booking.CheckOutUtc, booking.TotalAmount), null);
    }

    private async Task<(Guid PropertyId, TimeZoneInfo TimeZone, DateOnly TodayLocal, string Name, string Phone, PublicBookingError? Error)> GetRequestContextAsync(string rawName, string rawPhone, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().Where(x => x.Code == PublicPropertyCode && x.IsActive).Select(x => new { x.Id, x.TimeZoneId }).SingleOrDefaultAsync(ct);
        if (property is null) return (Guid.Empty, TimeZoneInfo.Utc, default, "", "", new("property_not_found", "Cơ sở hiện không khả dụng."));
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId); var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var name = rawName.Trim(); var phone = rawPhone.Trim();
        if (name.Length is < 2 or > 200) return (property.Id, timeZone, today, name, phone, new("validation", "Vui lòng nhập tên khách hàng hợp lệ."));
        if (CustomerService.NormalizePhone(phone).Length < 8) return (property.Id, timeZone, today, name, phone, new("validation", "Vui lòng nhập số điện thoại hợp lệ."));
        return (property.Id, timeZone, today, name, phone, null);
    }

    private static PublicBookingError? ValidateDateWindow(DateOnly date, DateOnly today) => date < today || date > today.AddYears(1) ? new("validation", "Chỉ có thể gửi yêu cầu cho ngày hôm nay đến 12 tháng tới.") : null;
    private static PublicBookingError? ValidateStayDates(DateOnly arrival, DateOnly departure, string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        if (arrival < today || arrival > today.AddYears(1)) return new("validation", "Ngày nhận phòng phải từ hôm nay đến 12 tháng tới.");
        var nights = departure.DayNumber - arrival.DayNumber;
        if (nights is < 1 or > 30) return new("validation", "Thời gian lưu trú phải từ 1 đến 30 đêm.");
        return null;
    }

    private async Task<HashSet<(Guid RoomId, Guid RateId)>> GetUnavailableRateKeysAsync(Guid propertyId, string timeZoneId, DateOnly date, CancellationToken ct)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); var windowStartLocal = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified); var windowEndLocal = windowStartLocal.AddDays(2);
        var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(windowStartLocal, timeZone); var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(windowEndLocal, timeZone);
        var locked = await db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId && LockingStatuses.Contains(x.Status) && x.CheckInUtc < windowEndUtc && windowStartUtc < x.CheckOutUtc).Select(x => new { x.RoomId, x.CheckInUtc, x.CheckOutUtc }).ToListAsync(ct);
        var rates = await db.RoomRates.AsNoTracking().Where(x => x.Room.PropertyId == propertyId && x.Room.IsActive && x.Room.IsPublished && x.IsActive && x.Type != RoomRateType.Nightly).Select(x => new { x.Id, x.RoomId, x.StartTime, x.EndTime, x.Type }).ToListAsync(ct);
        HashSet<(Guid RoomId, Guid RateId)> unavailable = [];
        foreach (var rate in rates) { var range = ToUtcTimeSlotRange(date, rate.StartTime, rate.EndTime, rate.Type == RoomRateType.Overnight, timeZone); if (locked.Any(x => x.RoomId == rate.RoomId && x.CheckInUtc < range.CheckOutUtc && range.CheckInUtc < x.CheckOutUtc)) unavailable.Add((rate.RoomId, rate.Id)); }
        return unavailable;
    }

    private static (DateTime CheckInUtc, DateTime CheckOutUtc) ToUtcTimeSlotRange(DateOnly date, TimeOnly start, TimeOnly end, bool overnight, TimeZoneInfo tz)
    {
        var inLocal = DateTime.SpecifyKind(date.ToDateTime(start), DateTimeKind.Unspecified); var outDate = overnight || end <= start ? date.AddDays(1) : date; var outLocal = DateTime.SpecifyKind(outDate.ToDateTime(end), DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(inLocal, tz), TimeZoneInfo.ConvertTimeToUtc(outLocal, tz));
    }
    private static (DateTime CheckInUtc, DateTime CheckOutUtc) ToUtcStayRange(DateOnly arrival, DateOnly departure, TimeOnly checkIn, TimeOnly checkOut, TimeZoneInfo tz)
    {
        var inLocal = DateTime.SpecifyKind(arrival.ToDateTime(checkIn), DateTimeKind.Unspecified); var outLocal = DateTime.SpecifyKind(departure.ToDateTime(checkOut), DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(inLocal, tz), TimeZoneInfo.ConvertTimeToUtc(outLocal, tz));
    }
    private static bool HasBathtub(string code) => code is "COCO-01" or "MOON-04" or "AMBER-05" or "ROMAN-06";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
