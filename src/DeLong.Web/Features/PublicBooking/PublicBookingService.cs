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
    private static readonly BookingStatus[] LockingStatuses =
        [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<PublicCatalogDto?> GetCatalogAsync(DateOnly? availabilityDate = null, CancellationToken cancellationToken = default)
    {
        var property = await db.Properties
            .AsNoTracking()
            .Where(x => x.Code == PublicPropertyCode && x.IsActive)
            .Select(x => new { x.Id, x.Name, x.TimeZoneId })
            .SingleOrDefaultAsync(cancellationToken);
        if (property is null) return null;

        HashSet<(Guid RoomId, Guid RateId)> unavailable = [];
        if (availabilityDate.HasValue)
        {
            unavailable = await GetUnavailableRateKeysAsync(property.Id, property.TimeZoneId, availabilityDate.Value, cancellationToken);
        }

        var rooms = await db.Rooms
            .AsNoTracking()
            .Where(x => x.PropertyId == property.Id && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Capacity,
                Rates = x.Rates.Where(r => r.IsActive).OrderBy(r => r.SortOrder).Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.StartTime,
                    r.EndTime,
                    r.IsOvernight,
                    r.Price
                }).ToList()
            })
            .ToListAsync(cancellationToken);

        var roomDtos = rooms.Select(room =>
        {
            var rates = room.Rates.Select(rate => new PublicRateDto(
                rate.Id,
                rate.Name,
                rate.StartTime.ToString("HH:mm"),
                rate.EndTime.ToString("HH:mm"),
                rate.IsOvernight,
                rate.Price,
                !availabilityDate.HasValue || !unavailable.Contains((room.Id, rate.Id)))).ToList();

            return new PublicRoomDto(
                room.Id,
                room.Code,
                room.Name,
                room.Capacity,
                HasBathtub(room.Code),
                rates.Count == 0 ? 0 : rates.Min(x => x.Price),
                rates);
        }).ToList();

        return new PublicCatalogDto(property.Id, property.Name, property.TimeZoneId, roomDtos);
    }

    public async Task<PublicRoomDto?> GetRoomAsync(string code, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(null, cancellationToken);
        return catalog?.Rooms.SingleOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<PublicAvailabilityDto?> GetAvailabilityAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogAsync(date, cancellationToken);
        return catalog is null ? null : new PublicAvailabilityDto(date.ToString("yyyy-MM-dd"), catalog.Rooms);
    }

    public async Task<(PublicBookingResult? Result, PublicBookingError? Error)> CreateRequestAsync(
        PublicBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
            return (null, new("spam", "Không thể gửi yêu cầu."));

        if (!DateOnly.TryParseExact(request.StayDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var stayDate))
            return (null, new("validation", "Ngày đặt phòng không hợp lệ."));

        var property = await db.Properties
            .AsNoTracking()
            .Where(x => x.Code == PublicPropertyCode && x.IsActive)
            .Select(x => new { x.Id, x.TimeZoneId })
            .SingleOrDefaultAsync(cancellationToken);
        if (property is null) return (null, new("property_not_found", "Cơ sở hiện không khả dụng."));

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        if (stayDate < todayLocal || stayDate > todayLocal.AddYears(1))
            return (null, new("validation", "Chỉ có thể gửi yêu cầu cho ngày hôm nay đến 12 tháng tới."));

        var name = request.CustomerName.Trim();
        var phone = request.CustomerPhone.Trim();
        if (name.Length is < 2 or > 200)
            return (null, new("validation", "Vui lòng nhập tên khách hàng hợp lệ."));
        if (CustomerService.NormalizePhone(phone).Length < 8)
            return (null, new("validation", "Vui lòng nhập số điện thoại hợp lệ."));

        var rate = await db.RoomRates
            .AsNoTracking()
            .Where(x => x.Id == request.RateId && x.RoomId == request.RoomId && x.IsActive && x.Room.IsActive && x.Room.PropertyId == property.Id)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.StartTime,
                x.EndTime,
                x.IsOvernight,
                x.Price,
                RoomId = x.Room.Id,
                RoomName = x.Room.Name
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (rate is null) return (null, new("rate_not_found", "Khung giờ hoặc phòng không còn khả dụng."));

        var (checkInUtc, checkOutUtc) = ToUtcRange(stayDate, rate.StartTime, rate.EndTime, rate.IsOvernight, timeZone);
        if (await bookingService.HasConflictAsync(property.Id, rate.RoomId, checkInUtc, checkOutUtc, null, cancellationToken))
            return (null, new("booking_conflict", "Phòng vừa được giữ hoặc xác nhận trong khung giờ này. Vui lòng chọn khung khác."));

        var (booking, error) = await bookingService.CreateAsync(
            property.Id,
            new CreateBookingRequest
            {
                RoomId = rate.RoomId,
                CustomerName = name,
                CustomerPhone = phone,
                CheckIn = new DateTimeOffset(checkInUtc, TimeSpan.Zero),
                CheckOut = new DateTimeOffset(checkOutUtc, TimeSpan.Zero),
                Status = BookingStatus.Requested,
                RoomAmount = rate.Price,
                ExtraAmount = 0,
                DiscountAmount = 0,
                Source = "Website",
                Note = Clean(request.Note)
            },
            actorUserId: null,
            cancellationToken);

        if (booking is null)
            return (null, new(error?.Code ?? "booking_failed", error?.Message ?? "Không thể tạo yêu cầu đặt phòng."));

        return (new PublicBookingResult(
            booking.Id,
            booking.Code,
            rate.RoomName,
            rate.Name,
            booking.CheckInUtc,
            booking.CheckOutUtc,
            booking.TotalAmount), null);
    }

    private async Task<HashSet<(Guid RoomId, Guid RateId)>> GetUnavailableRateKeysAsync(
        Guid propertyId,
        string timeZoneId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var windowStartLocal = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var windowEndLocal = windowStartLocal.AddDays(2);
        var windowStartUtc = TimeZoneInfo.ConvertTimeToUtc(windowStartLocal, timeZone);
        var windowEndUtc = TimeZoneInfo.ConvertTimeToUtc(windowEndLocal, timeZone);

        var locked = await db.Bookings
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && LockingStatuses.Contains(x.Status) && x.CheckInUtc < windowEndUtc && windowStartUtc < x.CheckOutUtc)
            .Select(x => new { x.RoomId, x.CheckInUtc, x.CheckOutUtc })
            .ToListAsync(cancellationToken);

        var rates = await db.RoomRates
            .AsNoTracking()
            .Where(x => x.Room.PropertyId == propertyId && x.Room.IsActive && x.IsActive)
            .Select(x => new { x.Id, x.RoomId, x.StartTime, x.EndTime, x.IsOvernight })
            .ToListAsync(cancellationToken);

        HashSet<(Guid RoomId, Guid RateId)> unavailable = [];
        foreach (var rate in rates)
        {
            var (checkInUtc, checkOutUtc) = ToUtcRange(date, rate.StartTime, rate.EndTime, rate.IsOvernight, timeZone);
            if (locked.Any(x => x.RoomId == rate.RoomId && x.CheckInUtc < checkOutUtc && checkInUtc < x.CheckOutUtc))
                unavailable.Add((rate.RoomId, rate.Id));
        }

        return unavailable;
    }

    private static (DateTime CheckInUtc, DateTime CheckOutUtc) ToUtcRange(
        DateOnly date,
        TimeOnly start,
        TimeOnly end,
        bool isOvernight,
        TimeZoneInfo timeZone)
    {
        var checkInLocal = DateTime.SpecifyKind(date.ToDateTime(start), DateTimeKind.Unspecified);
        var checkoutDate = isOvernight || end <= start ? date.AddDays(1) : date;
        var checkOutLocal = DateTime.SpecifyKind(checkoutDate.ToDateTime(end), DateTimeKind.Unspecified);
        return (TimeZoneInfo.ConvertTimeToUtc(checkInLocal, timeZone), TimeZoneInfo.ConvertTimeToUtc(checkOutLocal, timeZone));
    }

    private static bool HasBathtub(string code) => code is "COCO-01" or "MOON-04" or "AMBER-05";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
