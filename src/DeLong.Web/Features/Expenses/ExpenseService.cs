using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DeLong.Web.Common.Persistence;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Common.Auditing;

namespace DeLong.Web.Features.Expenses;

public sealed class ExpenseService(AppDbContext db, AuditService? audit = null)
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
        if (request.RoomId.HasValue && !await db.Rooms.AsNoTracking().AnyAsync(x => x.Id == request.RoomId && x.PropertyId == propertyId, cancellationToken))
            return (null, new("validation", "Phòng không thuộc chi nhánh."));
        if (request.RequestKey == Guid.Empty) return (null, new("validation", "Mã gửi lại không hợp lệ."));
        await using var transaction = request.RequestKey.HasValue ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var hash = request.RequestKey.HasValue ? RequestFingerprint.Create(request) : null;
        if (request.RequestKey.HasValue) {
            await RequestFingerprint.LockAsync(db, $"expense:{propertyId}:{request.RequestKey}", cancellationToken);
            var previous = await db.Expenses.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.RequestKey == request.RequestKey, cancellationToken);
            if (previous is not null) return previous.RequestHash == hash ? (ToDto(previous), null) : (null, new("retry_conflict", "Mã gửi lại đã dùng cho nội dung khác."));
        }

        var expense = new Expense
        {
            PropertyId = propertyId,
            RoomId = request.RoomId,
            RequestKey = request.RequestKey,
            RequestHash = hash,
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
        (audit ?? new AuditService(db)).Add(propertyId, "Expense", expense.Id, "Created", actorUserId,
            after: new { expense.OccurredAtUtc, expense.Amount, expense.Method, expense.RoomId, expense.Category, expense.Description, expense.Note });
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return (ToDto(expense), null);
    }

    public async Task<(ExpenseDto? Expense, ExpenseOperationError? Error)> VoidAsync(
        Guid propertyId,
        Guid expenseId,
        string reason,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000)
            return (null, new("validation", "Cần nhập lý do void khoản chi."));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await RequestFingerprint.LockAsync(db, $"expense-void:{propertyId}:{expenseId}", cancellationToken);
        var expense = await db.Expenses.SingleOrDefaultAsync(
            x => x.PropertyId == propertyId && x.Id == expenseId,
            cancellationToken);
        if (expense is null) return (null, new("not_found", "Không tìm thấy khoản chi."));
        if (expense.IsVoided) return (null, new("already_voided", "Khoản chi đã được void trước đó."));

        expense.IsVoided = true;
        expense.VoidedAtUtc = DateTime.UtcNow;
        expense.VoidedByUserId = actorUserId;
        expense.VoidReason = reason.Trim();
        (audit ?? new AuditService(db)).Add(propertyId, "Expense", expense.Id, "Voided", actorUserId, after: new { expense.VoidReason });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (ToDto(expense), null);
    }

    private static ExpenseOperationError? Validate(CreateExpenseRequest request)
    {
        if (!Enum.IsDefined(request.Method) || decimal.Round(request.Amount, 2) != request.Amount) return new("validation", "Phương thức hoặc số tiền không hợp lệ (tối đa 2 chữ số lẻ).");
        if (request.Note?.Length > 2000 || request.Reference?.Length > 200 || request.Vendor?.Length > 200) return new("validation", "Nội dung ghi chú/tham chiếu vượt giới hạn.");
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
        x.VoidedAtUtc, x.VoidedByUserId, x.VoidReason, x.RoomId);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
