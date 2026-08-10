using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Customers;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Bookings;

public sealed class BookingService(AppDbContext db, CustomerService customerService)
{
    private static readonly BookingStatus[] LockingStatuses =
    [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<IReadOnlyList<BookingDto>> GetAllAsync(
        Guid propertyId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId);
        if (from.HasValue)
        {
            var fromUtc = from.Value.UtcDateTime;
            query = query.Where(x => x.CheckOutUtc > fromUtc);
        }
        if (to.HasValue)
        {
            var toUtc = to.Value.UtcDateTime;
            query = query.Where(x => x.CheckInUtc < toUtc);
        }

        return await query
            .OrderBy(x => x.CheckInUtc)
            .Select(x => new BookingDto(
                x.Id, x.PropertyId, x.Code,
                x.RoomId, x.Room.Code, x.Room.Name,
                x.CustomerId, x.Customer.Name, x.Customer.Phone,
                x.CheckInUtc, x.CheckOutUtc, x.Status,
                x.RoomAmount, x.ExtraAmount, x.DiscountAmount,
                x.RoomAmount + x.ExtraAmount - x.DiscountAmount,
                x.Source, x.Note, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<BookingDto?> GetAsync(Guid propertyId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        return await db.Bookings
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Id == bookingId)
            .Select(x => new BookingDto(
                x.Id, x.PropertyId, x.Code,
                x.RoomId, x.Room.Code, x.Room.Name,
                x.CustomerId, x.Customer.Name, x.Customer.Phone,
                x.CheckInUtc, x.CheckOutUtc, x.Status,
                x.RoomAmount, x.ExtraAmount, x.DiscountAmount,
                x.RoomAmount + x.ExtraAmount - x.DiscountAmount,
                x.Source, x.Note, x.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<(BookingDto? Booking, BookingOperationError? Error)> CreateAsync(
        Guid propertyId,
        CreateBookingRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreate(request);
        if (validation is not null) return (null, validation);

        var roomExists = await db.Rooms.AnyAsync(
            x => x.PropertyId == propertyId && x.Id == request.RoomId && x.IsActive,
            cancellationToken);
        if (!roomExists)
            return (null, new("room_not_found", "Phòng không tồn tại hoặc đã ngừng hoạt động."));

        var checkInUtc = request.CheckIn.UtcDateTime;
        var checkOutUtc = request.CheckOut.UtcDateTime;
        if (BookingRules.LocksRoom(request.Status) &&
            await HasConflictAsync(propertyId, request.RoomId, checkInUtc, checkOutUtc, null, cancellationToken))
        {
            return (null, new("booking_conflict", "Phòng đã có booking trong khoảng thời gian này."));
        }

        var customer = await customerService.FindOrCreateEntityAsync(
            propertyId, request.CustomerId, request.CustomerName, request.CustomerPhone, cancellationToken);
        if (customer is null)
        {
            return (null, new("customer_invalid", "Không tìm thấy khách hàng hoặc thông tin khách chưa hợp lệ."));
        }

        var booking = new Booking
        {
            PropertyId = propertyId,
            RoomId = request.RoomId,
            Customer = customer,
            CheckInUtc = checkInUtc,
            CheckOutUtc = checkOutUtc,
            Status = request.Status,
            RoomAmount = request.RoomAmount,
            ExtraAmount = request.ExtraAmount,
            DiscountAmount = request.DiscountAmount,
            Source = Clean(request.Source),
            Note = Clean(request.Note)
        };
        booking.Code = $"BK-{DateTime.UtcNow:yyMMdd}-{booking.Id.ToString("N")[..6].ToUpperInvariant()}";

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(propertyId, booking.Id, cancellationToken), null);
    }

    public async Task<(BookingDto? Booking, BookingOperationError? Error)> ChangeStatusAsync(
        Guid propertyId,
        Guid bookingId,
        BookingStatus nextStatus,
        CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == bookingId,
            cancellationToken);
        if (booking is null) return (null, new("not_found", "Không tìm thấy booking."));

        if (!BookingRules.CanTransition(booking.Status, nextStatus))
        {
            return (null, new("invalid_transition", $"Không thể chuyển trạng thái từ {booking.Status} sang {nextStatus}."));
        }

        if (BookingRules.LocksRoom(nextStatus) &&
            await HasConflictAsync(propertyId, booking.RoomId, booking.CheckInUtc, booking.CheckOutUtc, booking.Id, cancellationToken))
        {
            return (null, new("booking_conflict", "Phòng đã có booking khác trong khoảng thời gian này."));
        }

        booking.Status = nextStatus;
        await db.SaveChangesAsync(cancellationToken);
        return (await GetAsync(propertyId, bookingId, cancellationToken), null);
    }

    public Task<bool> HasConflictAsync(
        Guid propertyId,
        Guid roomId,
        DateTime checkInUtc,
        DateTime checkOutUtc,
        Guid? excludeBookingId = null,
        CancellationToken cancellationToken = default)
    {
        return db.Bookings.AnyAsync(x =>
            x.PropertyId == propertyId &&
            x.RoomId == roomId &&
            LockingStatuses.Contains(x.Status) &&
            (!excludeBookingId.HasValue || x.Id != excludeBookingId.Value) &&
            x.CheckInUtc < checkOutUtc &&
            checkInUtc < x.CheckOutUtc,
            cancellationToken);
    }

    private static BookingOperationError? ValidateCreate(CreateBookingRequest request)
    {
        if (request.RoomId == Guid.Empty) return new("validation", "Vui lòng chọn phòng.");
        if (request.CheckOut <= request.CheckIn) return new("validation", "Giờ trả phòng phải sau giờ nhận phòng.");
        if (request.RoomAmount < 0 || request.ExtraAmount < 0 || request.DiscountAmount < 0)
            return new("validation", "Các khoản tiền không được âm.");
        if (request.RoomAmount + request.ExtraAmount - request.DiscountAmount < 0)
            return new("validation", "Tổng tiền booking không được âm.");
        if (request.Status is not (BookingStatus.Requested or BookingStatus.Held or BookingStatus.Confirmed))
            return new("validation", "Trạng thái tạo booking không hợp lệ.");
        if (!request.CustomerId.HasValue &&
            (string.IsNullOrWhiteSpace(request.CustomerName) || CustomerService.NormalizePhone(request.CustomerPhone).Length < 8))
            return new("validation", "Tên và số điện thoại khách là bắt buộc khi tạo khách mới.");
        return null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
