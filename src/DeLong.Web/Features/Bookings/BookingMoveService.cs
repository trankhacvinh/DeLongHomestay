using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeLong.Web.Features.Bookings;

public sealed class MoveBookingRequest
{
    public Guid RoomId { get; init; }
    public DateOnly TargetDate { get; init; }
}

public sealed class BookingMoveService(
    AppDbContext db,
    AuditService auditService,
    BookingService bookingService)
{
    private static readonly BookingStatus[] LockingStatuses =
        [BookingStatus.Held, BookingStatus.Confirmed, BookingStatus.CheckedIn];

    public async Task<(BookingDto? Booking, BookingOperationError? Error)> MoveAsync(
        Guid propertyId,
        Guid bookingId,
        MoveBookingRequest request,
        Guid? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (request.RoomId == Guid.Empty)
            return (null, new("validation", "Vui lòng chọn phòng đích."));

        var booking = await db.Bookings
            .SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Id == bookingId, cancellationToken);
        if (booking is null)
            return (null, new("not_found", "Không tìm thấy lượt đặt."));

        if (booking.Status is not (BookingStatus.Requested or BookingStatus.Held or BookingStatus.Confirmed))
            return (null, new("booking_move_not_allowed", "Chỉ lượt Yêu cầu, Giữ phòng hoặc Đã xác nhận mới được kéo trên lịch."));

        var targetRoom = await db.Rooms.AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.PropertyId == propertyId && x.Id == request.RoomId && (x.IsActive || x.Id == booking.RoomId),
                cancellationToken);
        if (targetRoom is null)
            return (null, new("room_not_found", "Phòng đích không tồn tại hoặc đã ngừng hoạt động."));

        var property = await db.Properties.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == propertyId, cancellationToken);
        if (property is null)
            return (null, new("not_found", "Không tìm thấy cơ sở."));

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return (null, new("timezone_invalid", "Múi giờ của cơ sở chưa được cấu hình đúng."));
        }
        catch (InvalidTimeZoneException)
        {
            return (null, new("timezone_invalid", "Múi giờ của cơ sở chưa được cấu hình đúng."));
        }

        var oldCheckInLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(booking.CheckInUtc, DateTimeKind.Utc), timeZone);
        var oldCheckOutLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(booking.CheckOutUtc, DateTimeKind.Utc), timeZone);
        var oldDate = DateOnly.FromDateTime(oldCheckInLocal);
        var roomChanged = request.RoomId != booking.RoomId;

        if (!roomChanged && request.TargetDate == oldDate)
            return (await bookingService.GetAsync(propertyId, bookingId, cancellationToken), null);

        DateTime newCheckInLocal;
        DateTime newCheckOutLocal;
        Guid? newRateId = booking.RoomRateId;
        string? newRateName = booking.RateName;

        if (booking.Type == BookingType.MultiDay && roomChanged)
        {
            if (!booking.NightCount.HasValue || booking.NightCount.Value <= 0)
                return (null, new("validation", "Lượt lưu trú nhiều ngày chưa có số đêm hợp lệ."));

            var nightlyRate = await db.RoomRates.AsNoTracking()
                .Where(x => x.RoomId == request.RoomId && x.IsActive && x.Type == RoomRateType.Nightly)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
            if (nightlyRate is null)
                return (null, new("target_room_no_nightly_rate", "Phòng đích chưa có mức giá “Lưu trú theo đêm” đang hoạt động."));

            newCheckInLocal = request.TargetDate.ToDateTime(nightlyRate.StartTime);
            newCheckOutLocal = request.TargetDate.AddDays(booking.NightCount.Value).ToDateTime(nightlyRate.EndTime);
            newRateId = nightlyRate.Id;
            newRateName = nightlyRate.Name;
        }
        else
        {
            var deltaDays = request.TargetDate.DayNumber - oldDate.DayNumber;
            newCheckInLocal = oldCheckInLocal.AddDays(deltaDays);
            newCheckOutLocal = oldCheckOutLocal.AddDays(deltaDays);

            if (booking.Type == BookingType.TimeSlot && roomChanged)
            {
                var startTime = TimeOnly.FromDateTime(newCheckInLocal);
                var endTime = TimeOnly.FromDateTime(newCheckOutLocal);
                var overnight = DateOnly.FromDateTime(newCheckOutLocal) > DateOnly.FromDateTime(newCheckInLocal);
                var matchingRate = await db.RoomRates.AsNoTracking()
                    .Where(x => x.RoomId == request.RoomId && x.IsActive && x.Type != RoomRateType.Nightly)
                    .Where(x => x.StartTime == startTime && x.EndTime == endTime)
                    .Where(x => overnight ? x.Type == RoomRateType.Overnight || x.IsOvernight : x.Type == RoomRateType.TimeSlot)
                    .OrderBy(x => x.SortOrder)
                    .FirstOrDefaultAsync(cancellationToken);
                newRateId = matchingRate?.Id;
                newRateName = matchingRate?.Name;
            }
        }

        DateTime newCheckInUtc;
        DateTime newCheckOutUtc;
        try
        {
            newCheckInUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(newCheckInLocal, DateTimeKind.Unspecified), timeZone);
            newCheckOutUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(newCheckOutLocal, DateTimeKind.Unspecified), timeZone);
        }
        catch (ArgumentException)
        {
            return (null, new("validation", "Thời gian đích không hợp lệ trong múi giờ của cơ sở."));
        }

        if (newCheckOutUtc <= newCheckInUtc)
            return (null, new("validation", "Giờ trả phòng phải sau giờ nhận phòng."));

        if (BookingRules.LocksRoom(booking.Status) && await HasConflictAsync(
                propertyId, request.RoomId, newCheckInUtc, newCheckOutUtc, booking.Id, cancellationToken))
            return (null, ConflictError());

        var before = Snapshot(booking);
        booking.RoomId = request.RoomId;
        booking.RoomRateId = newRateId;
        booking.RateName = newRateName;
        booking.CheckInUtc = newCheckInUtc;
        booking.CheckOutUtc = newCheckOutUtc;
        auditService.Add(propertyId, "Booking", booking.Id, "Moved", actorUserId, before, Snapshot(booking));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            return (null, ConflictError());
        }

        return (await bookingService.GetAsync(propertyId, bookingId, cancellationToken), null);
    }

    private Task<bool> HasConflictAsync(
        Guid propertyId,
        Guid roomId,
        DateTime checkInUtc,
        DateTime checkOutUtc,
        Guid bookingId,
        CancellationToken cancellationToken) =>
        db.Bookings.AnyAsync(x =>
            x.PropertyId == propertyId &&
            x.RoomId == roomId &&
            x.Id != bookingId &&
            LockingStatuses.Contains(x.Status) &&
            x.CheckInUtc < checkOutUtc &&
            checkInUtc < x.CheckOutUtc,
            cancellationToken);

    private static object Snapshot(Booking booking) => new
    {
        booking.Id,
        booking.Code,
        Type = booking.Type.ToString(),
        booking.RoomId,
        booking.RoomRateId,
        booking.RateName,
        booking.UnitPrice,
        booking.NightCount,
        booking.CheckInUtc,
        booking.CheckOutUtc,
        Status = booking.Status.ToString(),
        booking.RoomAmount,
        booking.ExtraAmount,
        booking.DiscountAmount
    };

    private static BookingOperationError ConflictError() =>
        new("booking_conflict", "Phòng đã có lượt đặt trong khoảng thời gian này.");
}
