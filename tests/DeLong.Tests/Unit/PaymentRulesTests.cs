using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Payments;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class PaymentRulesTests
{
    [Fact]
    public void Receipt_is_positive_and_refund_is_negative()
    {
        Assert.Equal(200_000m, PaymentRules.SignedAmount(PaymentType.Receipt, 200_000m));
        Assert.Equal(-200_000m, PaymentRules.SignedAmount(PaymentType.Refund, 200_000m));
    }
}
