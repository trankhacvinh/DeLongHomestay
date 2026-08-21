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
    private static readonly BookingStatus[] LockingStatuses = [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<IReadOnlyList<BookingDto>> GetAllAsync(Guid propertyId, DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        var query = db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId);
        if (from.HasValue) { var utc = from.Value.UtcDateTime; query = query.Where(x => x.CheckOutUtc > utc); }
        if (to.HasValue) { var utc = to.Value.UtcDateTime; query = query.Where(x => x.CheckInUtc < utc); }
        return await Project(query.OrderBy(x => x.CheckInUtc)).ToListAsync(cancellationToken);
    }

    public Task<BookingDto?> GetAsync(Guid propertyId, Guid bookingId, CancellationToken cancellationToken = default) =>
        Project(db.Bookings.AsNoTracking().Where(x => x.PropertyId == propertyId && x.Id == bookingId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<(BookingDto? Booking, BookingOperationError? Error)> CreateAsync(Guid propertyId, CreateBookingRequest request, Guid? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreate(request); if (validation is not null) return (null, validation);
        if (!await db.Rooms.AnyAsync(x => x.PropertyId == propertyId && x.Id == request.RoomId && x.IsActive, cancellationToken)) return (null, new("room_not_found", "Phòng không tồn tại hoặc đã ngừng hoạt động."));
        var rateError = await ValidateRateReferenceAsync(request.Type, request.RoomId, request.RoomRateId, cancellationToken);
        if (rateError is not null) return (null, rateError);

        var checkInUtc = request.CheckIn.UtcDateTime; var checkOutUtc = request.CheckOut.UtcDateTime;
        if (BookingRules.LocksRoom(request.Status) && await HasConflictAsync(propertyId, request.RoomId, checkInUtc, checkOutUtc, null, cancellationToken)) return (null, ConflictError());
        var customer = await customerService.FindOrCreateEntityAsync(propertyId, request.CustomerId, request.CustomerName, request.CustomerPhone, cancellationToken);
        if (customer is null) return (null, new("customer_invalid", "Không tìm thấy khách hàng hoặc thông tin khách chưa hợp lệ."));

        var booking = new Booking
        {
            PropertyId = propertyId, RoomId = request.RoomId, Customer = customer,
            Type = request.Type, RoomRateId = request.RoomRateId, RateName = Clean(request.RateName), UnitPrice = request.UnitPrice, NightCount = request.NightCount,
            CheckInUtc = checkInUtc, CheckOutUtc = checkOutUtc, Status = request.Status,
            RoomAmount = request.RoomAmount, ExtraAmount = request.ExtraAmount, DiscountAmount = request.DiscountAmount,
            Source = Clean(request.Source), PublicRequestKey = Clean(request.PublicRequestKey), Note = Clean(request.Note)
        };
        booking.Code = CreateBookingCode(booking.CreatedAtUtc);
        db.Bookings.Add(booking);
        auditService.Add(propertyId, "Booking", booking.Id, "Created", actorUserId, after: Snapshot(booking));
        var saveError = await SaveWithConflictGuardAsync(cancellationToken, !string.IsNullOrWhiteSpace(request.PublicRequestKey)); if (saveError is not null) return (null, saveError);
        return (await GetAsync(propertyId, booking.Id, cancellationToken), null);
    }

    public async Task<(BookingDto? Booking, BookingOperationError? Error)> UpdateAsync(Guid propertyId, Guid bookingId, UpdateBookingRequest request, Guid? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var validation = ValidateUpdate(request); if (validation is not null) return (null, validation);
        var booking = await db.Bookings.Include(x => x.Customer).SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken);
        if (booking is null) return (null, new("not_found", "Không tìm thấy booking."));
        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.NoShow) return (null, new("booking_locked", "Booking đã kết thúc nên không thể sửa thông tin vận hành."));

        // Calendar/editor V1 does not know about the nightly pricing snapshot. Do not allow an
        // older client to silently convert a MultiDay booking back to TimeSlot and erase its rate metadata.
        if (booking.Type == BookingType.MultiDay && request.Type != BookingType.MultiDay)
            return (null, new("multiday_edit_requires_v2", "Lượt lưu trú nhiều ngày phải được sửa bằng trình chỉnh sửa nhiều ngày."));

        if (!await db.Rooms.AnyAsync(x => x.PropertyId == propertyId && x.Id == request.RoomId && (x.IsActive || x.Id == booking.RoomId), cancellationToken)) return (null, new("room_not_found", "Phòng không tồn tại hoặc đã ngừng hoạt động."));
        var rateError = await ValidateRateReferenceAsync(request.Type, request.RoomId, request.RoomRateId, cancellationToken);
        if (rateError is not null) return (null, rateError);

        var customer = await db.Customers.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == request.CustomerId && x.IsActive, cancellationToken);
        if (customer is null) return (null, new("customer_invalid", "Không tìm thấy khách hàng."));
        var normalizedPhone = CustomerService.NormalizePhone(request.CustomerPhone);
        if (await db.Customers.AnyAsync(x => x.PropertyId == propertyId && x.NormalizedPhone == normalizedPhone && x.Id != customer.Id, cancellationToken)) return (null, new("customer_invalid", "Số điện thoại đang thuộc một khách hàng khác."));

        var checkInUtc = request.CheckIn.UtcDateTime; var checkOutUtc = request.CheckOut.UtcDateTime;
        if (BookingRules.LocksRoom(booking.Status) && await HasConflictAsync(propertyId, request.RoomId, checkInUtc, checkOutUtc, booking.Id, cancellationToken)) return (null, ConflictError());
        var before = Snapshot(booking);
        customer.Name = request.CustomerName.Trim(); customer.Phone = request.CustomerPhone.Trim(); customer.NormalizedPhone = normalizedPhone;
        booking.RoomId = request.RoomId; booking.CustomerId = customer.Id; booking.Type = request.Type; booking.RoomRateId = request.RoomRateId;
        booking.RateName = Clean(request.RateName); booking.UnitPrice = request.UnitPrice; booking.NightCount = request.NightCount;
        booking.CheckInUtc = checkInUtc; booking.CheckOutUtc = checkOutUtc; booking.RoomAmount = request.RoomAmount;
        booking.ExtraAmount = request.ExtraAmount; booking.DiscountAmount = request.DiscountAmount; booking.Source = Clean(request.Source); booking.Note = Clean(request.Note);
        auditService.Add(propertyId, "Booking", booking.Id, "Updated", actorUserId, before, Snapshot(booking));
        var saveError = await SaveWithConflictGuardAsync(cancellationToken); if (saveError is not null) return (null, saveError);
        return (await GetAsync(propertyId, bookingId, cancellationToken), null);
    }

    public async Task<(BookingDto? Booking, BookingOperationError? Error)> ChangeStatusAsync(Guid propertyId, Guid bookingId, BookingStatus nextStatus, Guid? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken);
        if (booking is null) return (null, new("not_found", "Không tìm thấy booking."));
        if (!BookingRules.CanTransition(booking.Status, nextStatus)) return (null, new("invalid_transition", $"Không thể chuyển trạng thái từ {booking.Status} sang {nextStatus}."));
        if (BookingRules.LocksRoom(nextStatus) && await HasConflictAsync(propertyId, booking.RoomId, booking.CheckInUtc, booking.CheckOutUtc, booking.Id, cancellationToken)) return (null, ConflictError());
        var before = Snapshot(booking); booking.Status = nextStatus;
        if (nextStatus == BookingStatus.Completed)
        {
            var room = await db.Rooms.SingleAsync(x => x.PropertyId == propertyId && x.Id == booking.RoomId, cancellationToken);
            room.HousekeepingStatus = HousekeepingStatus.Dirty; room.HousekeepingUpdatedAtUtc = DateTime.UtcNow; room.HousekeepingUpdatedByUserId = actorUserId;
            await AwardLoyaltyPointsAsync(booking, cancellationToken);
        }
        auditService.Add(propertyId, "Booking", booking.Id, "StatusChanged", actorUserId, before, Snapshot(booking));
        var saveError = await SaveWithConflictGuardAsync(cancellationToken); if (saveError is not null) return (null, saveError);
        return (await GetAsync(propertyId, bookingId, cancellationToken), null);
    }

    public Task<bool> HasConflictAsync(Guid propertyId, Guid roomId, DateTime checkInUtc, DateTime checkOutUtc, Guid? excludeBookingId = null, CancellationToken cancellationToken = default) =>
        db.Bookings.AnyAsync(x => x.PropertyId == propertyId && x.RoomId == roomId && LockingStatuses.Contains(x.Status) && (!excludeBookingId.HasValue || x.Id != excludeBookingId.Value) && x.CheckInUtc < checkOutUtc && checkInUtc < x.CheckOutUtc, cancellationToken);

    private async Task<BookingOperationError?> ValidateRateReferenceAsync(BookingType type, Guid roomId, Guid? rateId, CancellationToken cancellationToken)
    {
        if (!rateId.HasValue) return type == BookingType.MultiDay
            ? new("rate_not_found", "Lưu trú nhiều ngày phải chọn mức giá theo đêm của phòng.")
            : null;

        var rateType = await db.RoomRates.AsNoTracking()
            .Where(x => x.Id == rateId.Value && x.RoomId == roomId)
            .Select(x => (RoomRateType?)x.Type)
            .SingleOrDefaultAsync(cancellationToken);
        if (!rateType.HasValue) return new("rate_not_found", "Giá phòng không hợp lệ.");
        if (type == BookingType.MultiDay && rateType.Value != RoomRateType.Nightly)
            return new("rate_type_invalid", "Lưu trú nhiều ngày phải dùng mức giá “Lưu trú theo đêm”.");
        if (type == BookingType.TimeSlot && rateType.Value == RoomRateType.Nightly)
            return new("rate_type_invalid", "Đặt theo khung giờ không thể dùng mức giá lưu trú theo đêm.");
        return null;
    }

    private async Task<BookingOperationError?> SaveWithConflictGuardAsync(CancellationToken cancellationToken, bool publicRequest = false)
    {
        try { await db.SaveChangesAsync(cancellationToken); return null; }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.ExclusionViolation) { return ConflictError(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation && string.Equals(pg.ConstraintName, BookingCodeUniqueConstraint, StringComparison.OrdinalIgnoreCase)) { return new("booking_code_conflict", "Không thể tạo mã booking duy nhất. Vui lòng thử gửi lại yêu cầu."); }
        catch (DbUpdateException ex) when (publicRequest && ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation) { return new("public_request_retry", "Yêu cầu đã được nhận hoặc đang được xử lý. Vui lòng thử lại."); }
    }

    private static IQueryable<BookingDto> Project(IQueryable<Booking> query) => query.Select(x => new BookingDto(
        x.Id, x.PropertyId, x.Code, x.Type, x.RoomId, x.Room.Code, x.Room.Name, x.CustomerId, x.Customer.Name, x.Customer.Phone,
        x.RoomRateId, x.RateName, x.UnitPrice, x.NightCount, x.CheckInUtc, x.CheckOutUtc, x.Status,
        x.RoomAmount, x.ExtraAmount, x.DiscountAmount, x.RoomAmount + x.ExtraAmount - x.DiscountAmount,
        x.Payments.Where(p => !p.IsVoided).Sum(p => p.Type == PaymentType.Receipt ? p.Amount : -p.Amount),
        x.RoomAmount + x.ExtraAmount - x.DiscountAmount - x.Payments.Where(p => !p.IsVoided).Sum(p => p.Type == PaymentType.Receipt ? p.Amount : -p.Amount),
        x.Source, x.Note, x.CreatedAtUtc));

    private static object Snapshot(Booking b) => new { b.Id, b.Code, Type = b.Type.ToString(), b.RoomId, b.CustomerId, b.RoomRateId, b.RateName, b.UnitPrice, b.NightCount, b.CheckInUtc, b.CheckOutUtc, Status = b.Status.ToString(), b.RoomAmount, b.ExtraAmount, b.DiscountAmount, b.Source, b.Note };

    private async Task AwardLoyaltyPointsAsync(Booking booking, CancellationToken cancellationToken)
    {
        if (await db.LoyaltyLedgerEntries.AnyAsync(x => x.BookingId == booking.Id, cancellationToken)) return;
        var settings = await db.CustomerAccountSettings.AsNoTracking()
            .SingleOrDefaultAsync(x => x.PropertyId == booking.PropertyId && x.LoyaltyEnabled, cancellationToken);
        if (settings is null) return;
        var userId = await db.CustomerAccountLinks.AsNoTracking()
            .Where(x => x.PropertyId == booking.PropertyId && x.CustomerId == booking.CustomerId)
            .Select(x => (Guid?)x.UserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!userId.HasValue) return;
        var points = (int)decimal.Floor(booking.TotalAmount / settings.LoyaltySpendPerPoint);
        if (points <= 0) return;
        db.LoyaltyLedgerEntries.Add(new LoyaltyLedgerEntry
        {
            UserId = userId.Value,
            PropertyId = booking.PropertyId,
            BookingId = booking.Id,
            Points = points,
            Reason = $"Hoàn tất booking {booking.Code}"
        });
    }

    private static BookingOperationError? ValidateCreate(CreateBookingRequest r)
    {
        if (r.RoomId == Guid.Empty) return new("validation", "Vui lòng chọn phòng.");
        if (r.CheckOut <= r.CheckIn) return new("validation", "Giờ trả phòng phải sau giờ nhận phòng.");
        if (r.RoomAmount < 0 || r.ExtraAmount < 0 || r.DiscountAmount < 0 || r.RoomAmount + r.ExtraAmount - r.DiscountAmount < 0) return new("validation", "Các khoản tiền không hợp lệ.");
        if (r.Status is not (BookingStatus.Requested or BookingStatus.Held or BookingStatus.Confirmed)) return new("validation", "Trạng thái tạo booking không hợp lệ.");
        if (!r.CustomerId.HasValue && (string.IsNullOrWhiteSpace(r.CustomerName) || CustomerService.NormalizePhone(r.CustomerPhone).Length < 8)) return new("validation", "Tên và số điện thoại khách là bắt buộc khi tạo khách mới.");
        return ValidatePricingSnapshot(r.Type, r.RoomRateId, r.RateName, r.UnitPrice, r.NightCount, r.RoomAmount);
    }

    private static BookingOperationError? ValidateUpdate(UpdateBookingRequest r)
    {
        if (r.RoomId == Guid.Empty || r.CustomerId == Guid.Empty) return new("validation", "Phòng hoặc khách hàng không hợp lệ.");
        if (string.IsNullOrWhiteSpace(r.CustomerName) || CustomerService.NormalizePhone(r.CustomerPhone).Length < 8) return new("validation", "Thông tin khách hàng không hợp lệ.");
        if (r.CheckOut <= r.CheckIn) return new("validation", "Giờ trả phòng phải sau giờ nhận phòng.");
        if (r.RoomAmount < 0 || r.ExtraAmount < 0 || r.DiscountAmount < 0 || r.RoomAmount + r.ExtraAmount - r.DiscountAmount < 0) return new("validation", "Các khoản tiền không hợp lệ.");
        return ValidatePricingSnapshot(r.Type, r.RoomRateId, r.RateName, r.UnitPrice, r.NightCount, r.RoomAmount);
    }

    private static BookingOperationError? ValidatePricingSnapshot(BookingType type, Guid? rateId, string? rateName, decimal? unitPrice, int? nights, decimal roomAmount)
    {
        if (type != BookingType.MultiDay) return null;
        if (!rateId.HasValue || string.IsNullOrWhiteSpace(rateName) || !unitPrice.HasValue || unitPrice <= 0 || !nights.HasValue || nights <= 0) return new("validation", "Booking nhiều ngày phải có giá theo đêm và số đêm hợp lệ.");
        if (roomAmount != unitPrice.Value * nights.Value) return new("validation", "Tổng tiền phòng nhiều ngày không khớp giá theo đêm × số đêm.");
        return null;
    }

    private static string CreateBookingCode(DateTime createdAtUtc) { var token = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(); return $"BK-{createdAtUtc:yyMMdd}-{token}"; }
    private static BookingOperationError ConflictError() => new("booking_conflict", "Phòng đã có booking trong khoảng thời gian này.");
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
