using DeLong.Web.Data;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Expenses;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Finance;

public sealed record FinancePaymentDto(
    Guid Id,
    Guid PropertyId,
    Guid BookingId,
    string BookingCode,
    string CustomerName,
    PaymentType Type,
    PaymentMethod Method,
    decimal Amount,
    DateTime OccurredAtUtc,
    bool IsVoided,
    string? Reference);

public sealed record FinanceSummaryDto(
    decimal Receipts,
    decimal Refunds,
    decimal NetReceipts,
    decimal Expenses,
    decimal NetCashFlow,
    decimal Outstanding);

public sealed record FinanceSnapshotDto(
    FinanceSummaryDto Summary,
    IReadOnlyList<FinancePaymentDto> Payments,
    IReadOnlyList<ExpenseDto> Expenses);

public sealed class FinanceService(AppDbContext db)
{
    public async Task<FinanceSnapshotDto> GetSnapshotAsync(
        Guid propertyId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        var payments = await db.Payments
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new FinancePaymentDto(
                x.Id,
                x.PropertyId,
                x.BookingId,
                x.Booking.Code,
                x.Booking.Customer.Name,
                x.Type,
                x.Method,
                x.Amount,
                x.OccurredAtUtc,
                x.IsVoided,
                x.Reference))
            .ToListAsync(cancellationToken);

        var expenses = await db.Expenses
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new ExpenseDto(
                x.Id, x.PropertyId, x.OccurredAtUtc, x.Category, x.Description, x.Amount, x.Method,
                x.Vendor, x.Reference, x.Note, x.RecordedByUserId, x.IsVoided,
                x.VoidedAtUtc, x.VoidedByUserId, x.VoidReason))
            .ToListAsync(cancellationToken);

        var outstanding = await db.Bookings
            .AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Status != BookingStatus.Cancelled && x.Status != BookingStatus.NoShow)
            .Select(x => x.RoomAmount + x.ExtraAmount - x.DiscountAmount -
                         x.Payments.Where(p => !p.IsVoided).Sum(p => p.Type == PaymentType.Receipt ? p.Amount : -p.Amount))
            .Where(balance => balance > 0)
            .SumAsync(cancellationToken);

        var receipts = payments.Where(x => !x.IsVoided && x.Type == PaymentType.Receipt).Sum(x => x.Amount);
        var refunds = payments.Where(x => !x.IsVoided && x.Type == PaymentType.Refund).Sum(x => x.Amount);
        var expenseTotal = expenses.Where(x => !x.IsVoided).Sum(x => x.Amount);
        var netReceipts = receipts - refunds;

        return new FinanceSnapshotDto(
            new FinanceSummaryDto(
                receipts,
                refunds,
                netReceipts,
                expenseTotal,
                netReceipts - expenseTotal,
                outstanding),
            payments,
            expenses);
    }
}
