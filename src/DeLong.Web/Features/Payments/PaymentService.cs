using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Payments;

public sealed class PaymentService(AppDbContext db)
{
    public async Task<IReadOnlyList<PaymentDto>> GetByBookingAsync(
        Guid propertyId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return await db.Payments
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.BookingId == bookingId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetNetPaidAsync(
        Guid propertyId,
        Guid bookingId,
        CancellationToken cancellationToken = default)
    {
        return await db.Payments
            .Where(x => x.PropertyId == propertyId && x.BookingId == bookingId && !x.IsVoided)
            .SumAsync(x => x.Type == PaymentType.Receipt ? x.Amount : -x.Amount, cancellationToken);
    }

    public async Task<(PaymentDto? Payment, PaymentOperationError? Error)> AddAsync(
        Guid propertyId,
        Guid bookingId,
        CreatePaymentRequest request,
        Guid? recordedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0) return (null, new("validation", "Số tiền phải lớn hơn 0."));
        if (request.Amount > 10_000_000_000m) return (null, new("validation", "Số tiền vượt giới hạn cho phép."));

        var bookingExists = await db.Bookings.AnyAsync(
            x => x.PropertyId == propertyId && x.Id == bookingId,
            cancellationToken);
        if (!bookingExists) return (null, new("not_found", "Không tìm thấy booking."));

        if (request.Type == PaymentType.Refund)
        {
            var netPaid = await GetNetPaidAsync(propertyId, bookingId, cancellationToken);
            if (request.Amount > netPaid)
                return (null, new("refund_exceeds_paid", "Số tiền hoàn không được lớn hơn số tiền khách đã thanh toán."));
        }

        var payment = new Payment
        {
            PropertyId = propertyId,
            BookingId = bookingId,
            Type = request.Type,
            Method = request.Method,
            Amount = request.Amount,
            OccurredAtUtc = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow,
            Reference = Clean(request.Reference),
            Note = Clean(request.Note),
            RecordedByUserId = recordedByUserId
        };

        db.Payments.Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(payment), null);
    }

    public async Task<(PaymentDto? Payment, PaymentOperationError? Error)> VoidAsync(
        Guid propertyId,
        Guid paymentId,
        string reason,
        Guid? voidedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (null, new("validation", "Cần nhập lý do void giao dịch."));

        var payment = await db.Payments.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == paymentId,
            cancellationToken);
        if (payment is null) return (null, new("not_found", "Không tìm thấy giao dịch."));
        if (payment.IsVoided) return (null, new("already_voided", "Giao dịch đã được void trước đó."));

        if (payment.Type == PaymentType.Receipt)
        {
            var netPaid = await GetNetPaidAsync(propertyId, payment.BookingId, cancellationToken);
            if (netPaid - payment.Amount < 0)
            {
                return (null, new("void_breaks_balance", "Không thể void khoản thu này vì booking đã có khoản hoàn tiền liên quan."));
            }
        }

        payment.IsVoided = true;
        payment.VoidedAtUtc = DateTime.UtcNow;
        payment.VoidedByUserId = voidedByUserId;
        payment.VoidReason = reason.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(payment), null);
    }

    private static PaymentDto ToDto(Payment x) => new(
        x.Id,
        x.PropertyId,
        x.BookingId,
        x.Type,
        x.Method,
        x.Amount,
        PaymentRules.SignedAmount(x.Type, x.Amount),
        x.OccurredAtUtc,
        x.Reference,
        x.Note,
        x.RecordedByUserId,
        x.IsVoided,
        x.VoidedAtUtc,
        x.VoidedByUserId,
        x.VoidReason);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
