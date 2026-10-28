namespace DeLong.Web.Features.Cash;

public sealed record CashReceiptRequest(Guid RequestKey, DateTimeOffset OccurredAt, Guid? RoomId, decimal Amount, string Description, string? Note);
public sealed record CashVoidRequest(string Reason);
public sealed record CashEntryDto(Guid Id, string Source, Guid PropertyId, string PropertyName, Guid? RoomId, string? RoomName,
    DateTime OccurredAtUtc, string Description, string? Note, decimal Inflow, decimal Outflow, bool IsVoided, string? VoidReason,
    Guid? RecordedByUserId, string? RecordedByName = null, string? ExpenseCategory = null);
public sealed record CashTotalsDto(decimal BookingReceipts, decimal OtherReceipts, decimal Refunds, decimal Expenses, decimal Net);
public sealed record CashGroupDto(string Key, string Name, decimal Inflow, decimal Refunds, decimal Expenses, decimal Net);
public sealed record CashSnapshotDto(string Month, CashTotalsDto Totals, IReadOnlyList<CashEntryDto> Entries,
    IReadOnlyList<CashGroupDto> ByProperty, IReadOnlyList<CashGroupDto> ByRoom, IReadOnlyList<CashGroupDto> ByDay, IReadOnlyList<CashGroupDto> ByExpense);
