using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Domain.Entities;

/// <summary>Hồ sơ nhân viên tính lương. Không phải tài khoản đăng nhập.</summary>
public sealed class Employee : EntityBase
{
    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string? Phone { get; set; }
    public int ShiftsPerDay { get; set; } = 4;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool AttendanceBonusEligible { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Note { get; set; }
    public ICollection<EmployeeSalaryRate> SalaryRates { get; set; } = new List<EmployeeSalaryRate>();
}

/// <summary>Mức lương tháng có hiệu lực từ một ngày. Ca của ngày nào tính theo mức hiệu lực ngày đó.</summary>
public sealed class EmployeeSalaryRate : EntityBase
{
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public decimal MonthlySalary { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

/// <summary>Một ô chấm công: nhân viên × ngày × ca. Không có dòng = chưa chấm.</summary>
public sealed class AttendanceEntry : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly WorkDate { get; set; }
    public int Slot { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Note { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}

public sealed class PayrollAdjustment : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly Month { get; set; }
    public string Type { get; set; } = string.Empty;
    /// <summary>Dương = khoản tăng, âm = khoản giảm.</summary>
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid? CreatedByUserId { get; set; }
}

public sealed class PayrollHoliday : EntityBase
{
    public Guid PropertyId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public PayrollHolidayKind Kind { get; set; } = PayrollHolidayKind.Holiday;
}

public sealed class PayrollPolicy : EntityBase
{
    public Guid PropertyId { get; set; }
    public int SalaryDaysBase { get; set; } = 30;
    public decimal AttendanceBonus { get; set; } = 300_000m;
    public decimal HolidayMultiplier { get; set; } = 2m;
    public decimal TetMultiplier { get; set; } = 3m;
}

/// <summary>Kỳ lương của một cơ sở. Khi chốt, số liệu được lưu snapshot và chấm công bị khoá.</summary>
public sealed class PayrollPeriod : EntityBase
{
    public Guid PropertyId { get; set; }
    public DateOnly Month { get; set; }
    public PayrollPeriodStatus Status { get; set; } = PayrollPeriodStatus.Draft;
    public string? SnapshotJson { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public Guid? LockedByUserId { get; set; }
}

/// <summary>Ứng hoặc trả lương. Mỗi dòng tạo một phiếu chi (Expense) liên kết.</summary>
public sealed class PayrollTransaction : EntityBase
{
    public Guid PropertyId { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public DateOnly Month { get; set; }
    public PayrollTransactionKind Kind { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.Cash;
    public DateTime OccurredAtUtc { get; set; }
    public string? Note { get; set; }
    public Guid ExpenseId { get; set; }
    public Guid RequestKey { get; set; }
    public string RequestHash { get; set; } = string.Empty;
    public Guid? RecordedByUserId { get; set; }
    public bool IsVoided { get; set; }
    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidReason { get; set; }
}
