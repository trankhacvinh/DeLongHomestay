namespace DeLong.Web.Domain.Enums;

[Flags]
public enum VoucherApplicability
{
    None = 0,
    TimeSlot = 1,
    Overnight = 2,
    FullDay = 4
}

public enum VoucherStatus
{
    Draft = 0,
    Active = 1,
    Paused = 2,
    Archived = 3
}

public enum VoucherRedemptionStatus
{
    Reserved = 0,
    Redeemed = 1,
    Released = 2,
    ManuallyRestored = 3
}
