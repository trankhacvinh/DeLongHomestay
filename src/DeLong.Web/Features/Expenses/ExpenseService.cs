using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Expenses;

public sealed class ExpenseService(AppDbContext db)
{
    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(
        Guid propertyId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = db.Expenses.AsNoTracking().Where(x => x.PropertyId == propertyId);
        if (from.HasValue)
        {
            var value = from.Value.UtcDateTime;
            query = query.Where(x => x.OccurredAtUtc >= value);
        }
        if (to.HasValue)
        {
            var value = to.Value.UtcDateTime;
            query = query.Where(x => x.OccurredAtUtc < value);
        }

        return await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<(ExpenseDto? Expense, ExpenseOperationError? Error)> AddAsync(
        Guid propertyId,
        CreateExpenseRequest request,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request);
        if (validation is not null) return (null, validation);

        var expense = new Expense
        {
            PropertyId = propertyId,
            OccurredAtUtc = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow,
            Category = request.Category.Trim(),
            Description = request.Description.Trim(),
            Amount = request.Amount,
            Method = request.Method,
            Vendor = Clean(request.Vendor),
            Reference = Clean(request.Reference),
            Note = Clean(request.Note),
            RecordedByUserId = actorUserId
        };

        db.Expenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(expense), null);
    }

    public async Task<(ExpenseDto? Expense, ExpenseOperationError? Error)> VoidAsync(
        Guid propertyId,
        Guid expenseId,
        string reason,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (null, new("validation", "Cần nhập lý do void khoản chi."));

        var expense = await db.Expenses.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == expenseId,
            cancellationToken);
        if (expense is null) return (null, new("not_found", "Không tìm thấy khoản chi."));
        if (expense.IsVoided) return (null, new("already_voided", "Khoản chi đã được void trước đó."));

        expense.IsVoided = true;
        expense.VoidedAtUtc = DateTime.UtcNow;
        expense.VoidedByUserId = actorUserId;
        expense.VoidReason = reason.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return (ToDto(expense), null);
    }

    private static ExpenseOperationError? Validate(CreateExpenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category)) return new("validation", "Nhóm chi phí là bắt buộc.");
        if (request.Category.Trim().Length > 100) return new("validation", "Nhóm chi phí tối đa 100 ký tự.");
        if (string.IsNullOrWhiteSpace(request.Description)) return new("validation", "Nội dung chi phí là bắt buộc.");
        if (request.Description.Trim().Length > 500) return new("validation", "Nội dung chi phí tối đa 500 ký tự.");
        if (request.Amount <= 0) return new("validation", "Số tiền phải lớn hơn 0.");
        if (request.Amount > 10_000_000_000m) return new("validation", "Số tiền vượt giới hạn cho phép.");
        return null;
    }

    private static ExpenseDto ToDto(Expense x) => new(
        x.Id, x.PropertyId, x.OccurredAtUtc, x.Category, x.Description, x.Amount, x.Method,
        x.Vendor, x.Reference, x.Note, x.RecordedByUserId, x.IsVoided,
        x.VoidedAtUtc, x.VoidedByUserId, x.VoidReason);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
