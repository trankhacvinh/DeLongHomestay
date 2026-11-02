using System.Collections.Concurrent;
using System.Globalization;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Persistence;
using DeLong.Web.Common.Security;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Payments;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DeLong.Web.Features.Cancellations;

public sealed record CancellationScope(
    CurrentPropertyDto WorkingProperty,
    IReadOnlyList<CurrentPropertyDto> Accessible,
    IReadOnlyList<CurrentPropertyDto> Selected,
    bool IsAll);

public sealed class CancellationFilter
{
    /// <summary>Ngày bắt đầu (bao gồm), theo giờ địa phương của từng chi nhánh.</summary>
    public DateOnly From { get; init; }
    /// <summary>Ngày kết thúc (bao gồm), theo giờ địa phương của từng chi nhánh.</summary>
    public DateOnly To { get; init; }
    /// <summary>cancelled (ngày hủy), checkin (ngày nhận phòng), refund (ngày hoàn tiền).</summary>
    public string DateField { get; init; } = "cancelled";
    public string? Status { get; init; }
    public string? Search { get; init; }
    public Guid? ActorUserId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed record CancellationActorDto(Guid Id, string Name);

public sealed record CancellationResultDto(
    IReadOnlyList<CancellationRowDto> Rows,
    CancellationTotalsDto Totals,
    int TotalRows,
    int Page,
    int PageSize,
    IReadOnlyList<CancellationActorDto> Actors);

public sealed class CancellationService(AppDbContext db, PaymentService paymentService, AuditService audit)
{
    private static readonly ConcurrentDictionary<Guid, bool> BackfilledProperties = new();
    private static readonly string[] ValidDateFields = ["cancelled", "checkin", "refund"];
    private static readonly string[] ValidStatuses =
    [
        CancellationRefundStatuses.NotRequired, CancellationRefundStatuses.Pending, CancellationRefundStatuses.Partial,
        CancellationRefundStatuses.Refunded, CancellationRefundStatuses.Kept, "Open"
    ];

    public async Task<CancellationScope?> ResolveScopeAsync(
        System.Security.Claims.ClaimsPrincipal user,
        CurrentPropertyService properties,
        Guid? propertyId,
        string? scope,
        CancellationToken cancellationToken)
    {
        var working = await properties.ResolveAsync(user, propertyId, cancellationToken);
        if (working is null) return null;
        var accessible = await properties.GetAccessibleAsync(user, cancellationToken);
        var all = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) && accessible.Count > 1;
        if (all) return new(working, accessible, accessible, true);
        CurrentPropertyDto? selected = Guid.TryParse(scope, out var scopedId) ? accessible.SingleOrDefault(x => x.Id == scopedId) : null;
        if (!string.IsNullOrWhiteSpace(scope) && !string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) && selected is null) return null;
        selected ??= working;
        return new(working, accessible, [selected], false);
    }

    public async Task<(CancellationResultDto? Result, CancellationOperationError? Error)> ListAsync(
        CancellationScope scope,
        CancellationFilter filter,
        bool all,
        CancellationToken cancellationToken)
    {
        if (filter.To < filter.From) return (null, new("range_invalid", "Khoảng thời gian không hợp lệ."));
        if (filter.To.DayNumber - filter.From.DayNumber > 731) return (null, new("range_too_long", "Chỉ xem tối đa 2 năm mỗi lần."));
        var dateField = ValidDateFields.Contains(filter.DateField) ? filter.DateField : "cancelled";
        if (!string.IsNullOrWhiteSpace(filter.Status) && !ValidStatuses.Contains(filter.Status)) return (null, new("status_invalid", "Trạng thái hoàn tiền không hợp lệ."));

        var rows = new List<CancellationRowDto>();
        foreach (var property in scope.Selected)
        {
            await EnsureBackfilledAsync(property.Id, cancellationToken);
            rows.AddRange(await LoadPropertyRowsAsync(property, filter, dateField, cancellationToken));
        }

        var actors = rows.Where(x => x.CancelledByUserId.HasValue)
            .GroupBy(x => x.CancelledByUserId!.Value)
            .Select(g => new CancellationActorDto(g.Key, g.First().CancelledByName ?? "Không rõ"))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        IEnumerable<CancellationRowDto> filtered = rows;
        if (filter.ActorUserId.HasValue) filtered = filtered.Where(x => x.CancelledByUserId == filter.ActorUserId);
        if (!string.IsNullOrWhiteSpace(filter.Status))
            filtered = filter.Status == "Open"
                ? filtered.Where(x => x.RefundStatus is CancellationRefundStatuses.Pending or CancellationRefundStatuses.Partial)
                : filtered.Where(x => x.RefundStatus == filter.Status);
        var ordered = filtered.OrderByDescending(x => x.CancelledAtUtc).ThenBy(x => x.BookingCode).ToList();

        var totals = new CancellationTotalsDto(
            ordered.Count,
            ordered.Sum(x => x.TotalAmount),
            ordered.Sum(x => x.PaidAmount),
            ordered.Sum(x => x.RefundedAmount),
            ordered.Sum(x => x.RemainingRefundAmount),
            ordered.Sum(x => x.KeptAmount),
            ordered.Count(x => x.RefundStatus is CancellationRefundStatuses.Pending or CancellationRefundStatuses.Partial));

        var pageSize = Math.Clamp(filter.PageSize, 10, 200);
        var page = Math.Max(1, filter.Page);
        var paged = all ? ordered : ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (new CancellationResultDto(paged, totals, ordered.Count, page, pageSize, actors), null);
    }

    private async Task<List<CancellationRowDto>> LoadPropertyRowsAsync(
        CurrentPropertyDto property,
        CancellationFilter filter,
        string dateField,
        CancellationToken cancellationToken)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(property.TimeZoneId);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(filter.From.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(filter.To.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified), zone);

        var query = db.BookingCancellations.AsNoTracking()
            .Where(x => x.PropertyId == property.Id && x.RestoredAtUtc == null && x.Booking.Status == BookingStatus.Cancelled);
        query = dateField switch
        {
            "checkin" => query.Where(x => x.Booking.CheckInUtc >= fromUtc && x.Booking.CheckInUtc < toUtc),
            "refund" => query.Where(x => db.Payments.Any(p => p.BookingId == x.BookingId && p.Type == PaymentType.Refund && !p.IsVoided
                                                              && p.OccurredAtUtc >= fromUtc && p.OccurredAtUtc < toUtc)),
            _ => query.Where(x => x.CancelledAtUtc >= fromUtc && x.CancelledAtUtc < toUtc)
        };

        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var pattern = "%" + EscapeLike(search) + "%";
            query = query.Where(x => EF.Functions.ILike(x.Booking.Code, pattern, "\\")
                                     || EF.Functions.ILike(x.Booking.Customer.Name, pattern, "\\")
                                     || EF.Functions.ILike(x.Booking.Customer.Phone, pattern, "\\")
                                     || EF.Functions.ILike(x.Booking.Room.Name, pattern, "\\"));
        }

        var records = await query.OrderByDescending(x => x.CancelledAtUtc).Select(x => new
        {
            x.Id,
            x.BookingId,
            BookingCode = x.Booking.Code,
            BookingStatus = x.Booking.Status,
            CustomerName = x.Booking.Customer.Name,
            CustomerPhone = x.Booking.Customer.Phone,
            RoomName = x.Booking.Room.Name,
            x.Booking.CheckInUtc,
            x.Booking.CheckOutUtc,
            x.CancelledAtUtc,
            x.CancelledByUserId,
            x.Reason,
            x.IsBackfilled,
            x.TotalAmountSnapshot,
            x.PaidAmountSnapshot,
            x.RefundDueAmount,
            x.RefundNote
        }).ToListAsync(cancellationToken);
        if (records.Count == 0) return [];

        var bookingIds = records.Select(x => x.BookingId).ToArray();
        var refunds = await db.Payments.AsNoTracking()
            .Where(x => x.PropertyId == property.Id && bookingIds.Contains(x.BookingId) && x.Type == PaymentType.Refund && !x.IsVoided)
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.BookingId, x.Amount, x.Method, x.OccurredAtUtc, x.CreatedAtUtc, x.RecordedByUserId, x.Reference, x.Note })
            .ToListAsync(cancellationToken);

        var userIds = records.Where(x => x.CancelledByUserId.HasValue).Select(x => x.CancelledByUserId!.Value)
            .Concat(refunds.Where(x => x.RecordedByUserId.HasValue).Select(x => x.RecordedByUserId!.Value))
            .Distinct().ToArray();
        var names = userIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => !string.IsNullOrEmpty(x.DisplayName) ? x.DisplayName : (x.Email ?? x.UserName ?? "Không rõ"), cancellationToken);
        string? NameOf(Guid? id) => id.HasValue ? names.GetValueOrDefault(id.Value) : null;

        var refundsByBooking = refunds.ToLookup(x => x.BookingId);
        return records.Select(record =>
        {
            var items = refundsByBooking[record.BookingId]
                .Select(r => new CancellationRefundDto(r.Id, r.Amount, r.Method, r.OccurredAtUtc, r.CreatedAtUtc, r.RecordedByUserId, NameOf(r.RecordedByUserId), r.Reference, r.Note))
                .ToList();
            var refunded = items.Sum(x => x.Amount);
            var due = record.RefundDueAmount;
            var last = items.OrderByDescending(x => x.EnteredAtUtc).FirstOrDefault();
            return new CancellationRowDto(
                record.Id, property.Id, property.Name, record.BookingId, record.BookingCode, record.BookingStatus,
                record.CustomerName, record.CustomerPhone, record.RoomName, record.CheckInUtc, record.CheckOutUtc,
                record.CancelledAtUtc, record.CancelledByUserId, NameOf(record.CancelledByUserId), record.Reason, record.IsBackfilled,
                record.TotalAmountSnapshot, record.PaidAmountSnapshot, due, refunded,
                Math.Max(0, due - refunded),
                Math.Max(0, record.PaidAmountSnapshot - Math.Max(due, refunded)),
                CancellationRefundStatuses.Resolve(record.PaidAmountSnapshot, due, refunded),
                record.RefundNote, last?.OccurredAtUtc, last?.EnteredAtUtc, last?.RecordedByName, items);
        }).ToList();
    }

    /// <summary>Đổi số tiền cần hoàn cho khách (0 = giữ lại toàn bộ).</summary>
    public async Task<CancellationOperationError?> UpdateDueAsync(Guid propertyId, Guid cancellationId, UpdateRefundDueRequest request, Guid? actorUserId, CancellationToken cancellationToken)
    {
        if (request.Amount < 0) return new("validation", "Số tiền cần hoàn không được âm.");
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var record = await FindActiveAsync(propertyId, cancellationId, cancellationToken);
        if (record is null) return new("not_found", "Không tìm thấy hồ sơ hủy.");
        await RequestFingerprint.LockAsync(db, $"booking-payments:{record.BookingId}", cancellationToken);

        var received = await db.Payments.Where(x => x.BookingId == record.BookingId && x.Type == PaymentType.Receipt && !x.IsVoided)
            .SumAsync(x => x.Amount, cancellationToken);
        var limit = Math.Max(received, record.PaidAmountSnapshot);
        if (request.Amount > limit) return new("refund_exceeds_paid", $"Số tiền cần hoàn không được lớn hơn số khách đã thanh toán ({CancellationRecorder.Money(limit)}).");

        var before = new { record.RefundDueAmount, record.RefundNote };
        record.RefundDueAmount = request.Amount;
        record.RefundNote = Clean(request.Note) ?? record.RefundNote;
        audit.Add(propertyId, "Booking", record.BookingId, "CancellationRefundDueChanged", actorUserId, before, new { record.RefundDueAmount, record.RefundNote });
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return null;
    }

    /// <summary>Ghi nhận thêm một khoản hoàn tiền cho booking đã hủy.</summary>
    public async Task<CancellationOperationError?> AddRefundAsync(Guid propertyId, Guid cancellationId, RecordCancellationRefundRequest request, Guid? actorUserId, CancellationToken cancellationToken)
    {
        var record = await FindActiveAsync(propertyId, cancellationId, cancellationToken);
        if (record is null) return new("not_found", "Không tìm thấy hồ sơ hủy.");
        var (payment, error) = await paymentService.AddAsync(propertyId, record.BookingId, new CreatePaymentRequest
        {
            Type = PaymentType.Refund,
            Method = request.Method,
            Amount = request.Amount,
            OccurredAt = request.OccurredAt,
            Reference = request.Reference,
            Note = Clean(request.Note) ?? "Hoàn tiền do hủy booking"
        }, actorUserId, cancellationToken);
        if (error is not null) return new(error.Code, error.Message);
        await RaiseDueToRefundedAsync(record, cancellationToken);
        return null;
    }

    /// <summary>Sửa số tiền, phương thức, thời điểm hoặc ghi chú của một khoản hoàn đã nhập (có ghi lịch sử).</summary>
    public async Task<CancellationOperationError?> EditRefundAsync(Guid propertyId, Guid cancellationId, Guid paymentId, EditCancellationRefundRequest request, Guid? actorUserId, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) return new("validation", "Số tiền hoàn phải lớn hơn 0.");
        if (request.Amount > 10_000_000_000m) return new("validation", "Số tiền vượt giới hạn cho phép.");
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var record = await FindActiveAsync(propertyId, cancellationId, cancellationToken);
        if (record is null) return new("not_found", "Không tìm thấy hồ sơ hủy.");
        await RequestFingerprint.LockAsync(db, $"booking-payments:{record.BookingId}", cancellationToken);

        var payment = await db.Payments.SingleOrDefaultAsync(x => x.Id == paymentId && x.PropertyId == propertyId && x.BookingId == record.BookingId, cancellationToken);
        if (payment is null || payment.Type != PaymentType.Refund) return new("not_found", "Không tìm thấy khoản hoàn tiền.");
        if (payment.IsVoided) return new("already_voided", "Khoản hoàn này đã bị hủy nên không sửa được.");

        var netPaid = await paymentService.GetNetPaidAsync(propertyId, record.BookingId, cancellationToken);
        if (request.Amount > netPaid + payment.Amount)
            return new("refund_exceeds_paid", "Số tiền hoàn không được lớn hơn số tiền khách đã thanh toán.");

        var before = new { PaymentId = payment.Id, PaymentMethod = payment.Method.ToString(), PaymentAmount = payment.Amount, payment.OccurredAtUtc, payment.Reference, payment.Note };
        payment.Amount = request.Amount;
        payment.Method = request.Method;
        if (request.OccurredAt.HasValue) payment.OccurredAtUtc = request.OccurredAt.Value.UtcDateTime;
        payment.Reference = Clean(request.Reference);
        payment.Note = Clean(request.Note);
        audit.Add(propertyId, "Booking", record.BookingId, "PaymentRefundEdited", actorUserId, before,
            new { PaymentId = payment.Id, PaymentMethod = payment.Method.ToString(), PaymentAmount = payment.Amount, payment.OccurredAtUtc, payment.Reference, payment.Note });
        await db.SaveChangesAsync(cancellationToken);
        await RaiseDueToRefundedAsync(record, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return null;
    }

    private async Task RaiseDueToRefundedAsync(BookingCancellation record, CancellationToken cancellationToken)
    {
        var refunded = await db.Payments.Where(x => x.BookingId == record.BookingId && x.Type == PaymentType.Refund && !x.IsVoided)
            .SumAsync(x => x.Amount, cancellationToken);
        if (refunded <= record.RefundDueAmount) return;
        record.RefundDueAmount = refunded;
        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<BookingCancellation?> FindActiveAsync(Guid propertyId, Guid cancellationId, CancellationToken cancellationToken) =>
        db.BookingCancellations.SingleOrDefaultAsync(x => x.Id == cancellationId && x.PropertyId == propertyId && x.RestoredAtUtc == null, cancellationToken);

    /// <summary>
    /// Dựng hồ sơ cho các booking đã hủy thủ công từ trước khi có chức năng này, dựa vào lịch sử thao tác.
    /// Booking hủy tự động do quá hạn thanh toán không có dòng "StatusChanged" nên không được đưa vào danh sách.
    /// </summary>
    private async Task EnsureBackfilledAsync(Guid propertyId, CancellationToken cancellationToken)
    {
        if (BackfilledProperties.ContainsKey(propertyId)) return;
        try
        {
            var missing = await db.Bookings.AsNoTracking()
                .Where(b => b.PropertyId == propertyId && b.Status == BookingStatus.Cancelled
                            && !db.BookingCancellations.Any(c => c.BookingId == b.Id && c.RestoredAtUtc == null))
                .Select(b => new { b.Id, b.RoomAmount, b.SpecialSurchargeAmount, b.ExtraAmount, b.DiscountAmount })
                .ToListAsync(cancellationToken);
            if (missing.Count > 0)
            {
                var ids = missing.Select(x => x.Id).ToArray();
                // AfterJson là cột jsonb nên lọc nội dung ở phía ứng dụng.
                var logs = (await db.AuditLogs.AsNoTracking()
                    .Where(a => a.PropertyId == propertyId && a.EntityType == "Booking" && ids.Contains(a.EntityId)
                                && a.Action == "StatusChanged" && a.AfterJson != null)
                    .Select(a => new { a.EntityId, a.ActorUserId, a.CreatedAtUtc, a.AfterJson })
                    .ToListAsync(cancellationToken))
                    .Where(a => AfterStatusIsCancelled(a.AfterJson!))
                    .ToList();
                var latest = logs.GroupBy(x => x.EntityId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAtUtc).First());
                var money = await db.Payments.AsNoTracking()
                    .Where(p => p.PropertyId == propertyId && ids.Contains(p.BookingId) && !p.IsVoided)
                    .GroupBy(p => p.BookingId)
                    .Select(g => new { BookingId = g.Key, Received = g.Sum(p => p.Type == PaymentType.Receipt ? p.Amount : 0), Refunded = g.Sum(p => p.Type == PaymentType.Refund ? p.Amount : 0) })
                    .ToDictionaryAsync(x => x.BookingId, cancellationToken);
                foreach (var booking in missing)
                {
                    if (!latest.TryGetValue(booking.Id, out var log)) continue;
                    var received = money.TryGetValue(booking.Id, out var m) ? m.Received : 0;
                    db.BookingCancellations.Add(new BookingCancellation
                    {
                        PropertyId = propertyId,
                        BookingId = booking.Id,
                        CancelledAtUtc = log.CreatedAtUtc,
                        CancelledByUserId = log.ActorUserId,
                        TotalAmountSnapshot = booking.RoomAmount + booking.SpecialSurchargeAmount + booking.ExtraAmount - booking.DiscountAmount,
                        PaidAmountSnapshot = received,
                        RefundDueAmount = received,
                        IsBackfilled = true
                    });
                }
                if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
            }
            BackfilledProperties[propertyId] = true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Một yêu cầu khác vừa dựng xong cùng hồ sơ.
            foreach (var entry in db.ChangeTracker.Entries<BookingCancellation>().Where(x => x.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            BackfilledProperties[propertyId] = true;
        }
    }

    private static bool AfterStatusIsCancelled(string afterJson)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(afterJson);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                   && document.RootElement.TryGetProperty("status", out var status)
                   && status.ValueKind == System.Text.Json.JsonValueKind.String
                   && string.Equals(status.GetString(), "Cancelled", StringComparison.OrdinalIgnoreCase);
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
    }

    public static string DateText(DateTime utc, string timeZoneId) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(timeZoneId))
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
