namespace DeLong.Web.Domain.Enums;

public enum AttendanceStatus
{
    /// <summary>x — đi làm.</summary>
    Present = 0,
    /// <summary>K — nghỉ không lương: trừ tiền ca, mất chuyên cần.</summary>
    UnpaidLeave = 1,
    /// <summary>P — nghỉ có lương: không trừ tiền, mất chuyên cần.</summary>
    PaidLeave = 2,
    /// <summary>L — nghỉ lễ: không trừ tiền, không mất chuyên cần.</summary>
    HolidayOff = 3,
    /// <summary>B — nghỉ nhưng đã làm bù: không trừ tiền, mất chuyên cần.</summary>
    MadeUp = 4
}

public enum PayrollHolidayKind
{
    Holiday = 0,
    Tet = 1
}

public enum PayrollPeriodStatus
{
    Draft = 0,
    Locked = 1
}

public enum PayrollTransactionKind
{
    Advance = 0,
    Payment = 1
}
