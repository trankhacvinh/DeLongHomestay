using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Payments;

public static class PaymentRules
{
    public static decimal SignedAmount(PaymentType type, decimal amount) =>
        type == PaymentType.Receipt ? amount : -amount;
}
