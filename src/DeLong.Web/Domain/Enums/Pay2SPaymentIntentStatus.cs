namespace DeLong.Web.Domain.Enums;

public enum Pay2SPaymentIntentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Expired = 3,
    PaidAfterExpiry = 4
}
