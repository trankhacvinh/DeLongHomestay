using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Customers;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeLong.Web.Features.Bookings;

public sealed class BookingService(AppDbContext db, CustomerService customerService, AuditService auditService)
{
    private const string BookingCodeUniqueConstraint = "i_x_bookings_property_id_code";
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

        return await Project(query.OrderBy(x => x.CheckInUtc)).ToListAsync(cancellationToken);
    }

    public Task<BookingDto?> GetAsync(Guid propertyId, Guid bookingId, CancellationToken cancellationToken = default)
    {
        var query = db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId && x.Id == bookingId);
        return Project(query).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<(BookingDto? Booking, BookingOperationError? Error)> CreateAsync(
        Guid propertyId,
        CreateBookingRequest request,
        Guid? actorUserId = null,
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
            return (null, ConflictError());
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
        booking.Code = CreateBookingCode(booking.CreatedAtUtc);

        db.Bookings.Add(booking);
        auditService.Add(propertyId, "Booking", booking.Id, "Created", actorUserId, after: Snapshot(booking));
        var saveError = await SaveWithConflictGuardAsync(cancellationToken);
        if (saveError is not null) return (null, saveError);

        return (await GetAsync(propertyId, booking.Id, cancellationToken), null);
    }

    public async Task<(BookingDto? Booking, BookingOperationError? Error)> UpdateAsync(
        Guid propertyId,
        Guid bookingId,
        UpdateBookingRequest request,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateUpdate(request);
        if (validation is not null) return (null, validation);

        var booking = await db.Bookings
            .Include(x => x.Customer)
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken);
        if (booking is null) return (null, new("not_found", "Không tìm thấy booking."));
        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.NoShow)
            return (null, new("booking_locked", "Booking đã kết thúc nên không thể sửa thông tin vận hành."));

        var roomExists = await db.Rooms.AnyAsync(
            x => x.PropertyId == propertyId && x.Id == request.RoomId && (x.IsActive || x.Id == booking.RoomId),
            cancellationToken);
        if (!roomExists) return (null, new("room_not_found", "Phòng không tồn tại hoặc đã ngừng hoạt động."));

        var customer = await db.Customers.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == request.CustomerId && x.IsActive,
            cancellationToken);
        if (customer is null) return (null, new("customer_invalid", "Không tìm thấy khách hàng."));

        var normalizedPhone = CustomerService.NormalizePhone(request.CustomerPhone);
        if (await db.Customers.AnyAsync(
                x => x.PropertyId == propertyId && x.NormalizedPhone == normalizedPhone && x.Id != customer.Id,
                cancellationToken))
        {
            return (null, new("customer_invalid", "Số điện thoại đang thuộc một khách hàng khác."));
        }

        var checkInUtc = request.CheckIn.UtcDateTime;
        var checkOutUtc = request.CheckOut.UtcDateTime;
        if (BookingRules.LocksRoom(booking.Status) &&
            await HasConflictAsync(propertyId, request.RoomId, checkInUtc, checkOutUtc, booking.Id, cancellationToken))
        {
            return (null, ConflictError());
        }

        var before = Snapshot(booking);

        customer.Name = request.CustomerName.Trim();
        customer.Phone = request.CustomerPhone.Trim();
        customer.NormalizedPhone = normalizedPhone;

        booking.RoomId = request.RoomId;
        booking.CustomerId = customer.Id;
        booking.CheckInUtc = checkInUtc;
        booking.CheckOutUtc = checkOutUtc;
        booking.RoomAmount = request.RoomAmount;
        booking.ExtraAmount = request.ExtraAmount;
        booking.DiscountAmount = request.DiscountAmount;
        booking.Source = Clean(request.Source);
        booking.Note = Clean(request.Note);

        auditService.Add(propertyId, "Booking", booking.Id, "Updated", actorUserId, before, Snapshot(booking));
        var saveError = await SaveWithConflictGuardAsync(cancellationToken);
        if (saveError is not null) return (null, saveError);

        return (await GetAsync(propertyId, bookingId, cancellationToken), null);
    }

    public async Task<(BookingDto? Booking, BookingOperationError? Error)> ChangeStatusAsync(
        Guid propertyId,
        Guid bookingId,
        BookingStatus nextStatus,
        Guid? actorUserId = null,
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
            return (null, ConflictError());
        }

        var before = Snapshot(booking);
        booking.Status = nextStatus;

        if (nextStatus == BookingStatus.Completed)
        {
            var room = await db.Rooms.SingleAsync(
                x => x.PropertyId == propertyId && x.Id == booking.RoomId,
                cancellationToken);
            room.HousekeepingStatus = HousekeepingStatus.Dirty;
            room.HousekeepingUpdatedAtUtc = DateTime.UtcNow;
            room.HousekeepingUpdatedByUserId = actorUserId;
        }

        auditService.Add(propertyId, "Booking", booking.Id, "StatusChanged", actorUserId, before, Snapshot(booking));
        var saveError = await SaveWithConflictGuardAsync(cancellationToken);
        if (saveError is not null) return (null, saveError);

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

    private async Task<BookingOperationError?> SaveWithConflictGuardAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            return ConflictError();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(postgresException.ConstraintName, BookingCodeUniqueConstraint, StringComparison.OrdinalIgnoreCase))
        {
            return new("booking_code_conflict", "Không thể tạo mã booking duy nhất. Vui lòng thử gửi lại yêu cầu.");
        }
    }

    private static IQueryable<BookingDto> Project(IQueryable<Booking> query)
    {
        return query.Select(x => new BookingDto(
            x.Id,
            x.PropertyId,
            x.Code,
            x.RoomId,
            x.Room.Code,
            x.Room.Name,
            x.CustomerId,
            x.Customer.Name,
            x.Customer.Phone,
            x.CheckInUtc,
            x.CheckOutUtc,
            x.Status,
            x.RoomAmount,
            x.ExtraAmount,
            x.DiscountAmount,
            x.RoomAmount + x.ExtraAmount - x.DiscountAmount,
            x.Payments.Where(p => !p.IsVoided).Sum(p => p.Type == PaymentType.Receipt ? p.Amount : -p.Amount),
            x.RoomAmount + x.ExtraAmount - x.DiscountAmount -
                x.Payments.Where(p => !p.IsVoided).Sum(p => p.Type == PaymentType.Receipt ? p.Amount : -p.Amount),
            x.Source,
            x.Note,
            x.CreatedAtUtc));
    }

    private static object Snapshot(Booking booking) => new
    {
        booking.Id,
        booking.Code,
        booking.RoomId,
        booking.CustomerId,
        booking.CheckInUtc,
        booking.CheckOutUtc,
        Status = booking.Status.ToString(),
        booking.RoomAmount,
        booking.ExtraAmount,
        booking.DiscountAmount,
        booking.Source,
        booking.Note
    };

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

    private static BookingOperationError? ValidateUpdate(UpdateBookingRequest request)
    {
        if (request.RoomId == Guid.Empty) return new("validation", "Vui lòng chọn phòng.");
        if (request.CustomerId == Guid.Empty) return new("validation", "Khách hàng không hợp lệ.");
        if (string.IsNullOrWhiteSpace(request.CustomerName)) return new("validation", "Tên khách hàng là bắt buộc.");
        if (CustomerService.NormalizePhone(request.CustomerPhone).Length < 8) return new("validation", "Số điện thoại không hợp lệ.");
        if (request.CheckOut <= request.CheckIn) return new("validation", "Giờ trả phòng phải sau giờ nhận phòng.");
        if (request.RoomAmount < 0 || request.ExtraAmount < 0 || request.DiscountAmount < 0)
            return new("validation", "Các khoản tiền không được âm.");
        if (request.RoomAmount + request.ExtraAmount - request.DiscountAmount < 0)
            return new("validation", "Tổng tiền booking không được âm.");
        return null;
    }

    private static string CreateBookingCode(DateTime createdAtUtc)
    {
        // UUIDv7 prefixes are timestamp-ordered, so using the first characters of Booking.Id
        // causes deterministic collisions for bookings created in the same time window.
        // A v4 token keeps the human-readable date prefix while providing 40 random bits.
        var token = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return $"BK-{createdAtUtc:yyMMdd}-{token}";
    }

    private static BookingOperationError ConflictError() =>
        new("booking_conflict", "Phòng đã có booking trong khoảng thời gian này.");

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
