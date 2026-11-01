using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Payroll;

public sealed record PayrollEmployeeDto(
    Guid Id,
    string Code,
    string FullName,
    string? Position,
    string? Phone,
    int ShiftsPerDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool AttendanceBonusEligible,
    bool IsActive,
    int SortOrder,
    string? Note,
    decimal? MonthlySalary,
    IReadOnlyList<PayrollSalaryRateDto>? SalaryRates);

public sealed record PayrollSalaryRateDto(Guid Id, DateOnly EffectiveFrom, decimal MonthlySalary);

public sealed record AttendanceCellDto(Guid EmployeeId, DateOnly Date, int Slot, AttendanceStatus Status, string? Note);

public sealed record PayrollHolidayDto(Guid Id, DateOnly Date, string Name, PayrollHolidayKind Kind);

public sealed record PayrollPolicyDto(int SalaryDaysBase, decimal AttendanceBonus, decimal HolidayMultiplier, decimal TetMultiplier);

public sealed record PayrollAdjustmentDto(Guid Id, Guid EmployeeId, string Type, decimal Amount, string Reason, DateTime CreatedAtUtc);

public sealed record PayrollTransactionDto(
    Guid Id,
    Guid EmployeeId,
    PayrollTransactionKind Kind,
    decimal Amount,
    PaymentMethod Method,
    DateTime OccurredAtUtc,
    string? Note,
    Guid ExpenseId,
    bool IsVoided,
    string? VoidReason);

public sealed record PayrollPeriodDto(PayrollPeriodStatus Status, DateTime? LockedAtUtc, string? LockedByName);

public sealed record PayrollSheetDto(
    string Month,
    int DaysInMonth,
    bool CanManagePayroll,
    PayrollPeriodDto Period,
    IReadOnlyList<PayrollHolidayDto> Holidays,
    IReadOnlyList<PayrollEmployeeDto> Employees,
    IReadOnlyList<AttendanceCellDto> Attendance,
    PayrollPolicyDto? Policy,
    IReadOnlyList<PayrollLine>? Lines,
    IReadOnlyList<PayrollAdjustmentDto>? Adjustments,
    IReadOnlyList<PayrollTransactionDto>? Transactions);

public sealed record AttendanceCellInput(Guid EmployeeId, DateOnly Date, int Slot, AttendanceStatus? Status, string? Note);

public sealed record SaveAttendanceRequest(string Month, IReadOnlyList<AttendanceCellInput> Cells);

public sealed record SaveEmployeeRequest(
    string Code,
    string FullName,
    string? Position,
    string? Phone,
    int ShiftsPerDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool AttendanceBonusEligible,
    bool IsActive,
    int SortOrder,
    string? Note,
    decimal MonthlySalary,
    DateOnly? SalaryEffectiveFrom);

public sealed record SaveAdjustmentRequest(string Month, Guid EmployeeId, string Type, decimal Amount, string Reason);

public sealed record SaveHolidayRequest(DateOnly Date, string Name, PayrollHolidayKind Kind);

public sealed record PayrollMonthRequest(string Month);

public sealed record UnlockPayrollRequest(string Month, string Reason);

public sealed record CreatePayrollTransactionRequest(
    Guid RequestKey,
    Guid EmployeeId,
    string Month,
    PayrollTransactionKind Kind,
    decimal Amount,
    PaymentMethod Method,
    DateTimeOffset? OccurredAt,
    string? Note);

public sealed record VoidPayrollTransactionRequest(string Reason);

public sealed record PayrollError(string Code, string Message);
