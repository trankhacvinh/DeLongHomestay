using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.DataQuality;
using Xunit;

namespace DeLong.Tests.Unit;

public sealed class DataQualityRulesTests
{
    private static BookingQualityFact Booking(BookingStatus status, decimal paid = 0) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "BKTEST", "Blue", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddHours(3), status, 250000, paid);

    [Fact]
    public void Completed_balance_and_cancelled_receipt_are_review_signals_not_automatic_financial_changes()
    {
        var debt = Booking(BookingStatus.Completed, 100000);
        var cancelled = Booking(BookingStatus.Cancelled, 250000);
        var paid = Booking(BookingStatus.Completed, 250000);
        var issues = DataQualityRules.Evaluate([debt, cancelled, paid]);
        Assert.Equal(new[] { "completed_balance", "cancelled_receipt" }, issues.Select(x => x.Rule));
        Assert.Equal(100000, debt.Paid); Assert.Equal(BookingStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public void Duplicate_completed_stays_detected_but_cancelled_and_exact_boundary_are_excluded()
    {
        var a = Booking(BookingStatus.Completed);
        var duplicate = a with { Id = Guid.NewGuid(), Code = "BKOTHER", Paid = 250000 };
        var next = a with { Id = Guid.NewGuid(), CheckInUtc = a.CheckOutUtc, CheckOutUtc = a.CheckOutUtc.AddHours(3) };
        var cancelled = a with { Id = Guid.NewGuid(), Status = BookingStatus.Cancelled };
        var issues = DataQualityRules.Evaluate([a, duplicate, next, cancelled]);
        var overlap = Assert.Single(issues, x => x.Rule == "overlap");
        Assert.Contains("Nghi trùng", overlap.Title);
        Assert.Equal(overlap.Fingerprint, Assert.Single(DataQualityRules.Evaluate([duplicate, a]), x => x.Rule == "overlap").Fingerprint);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(250000, -1)]
    [InlineData(250000, 300000)]
    public void Invalid_money_is_detected(decimal total, decimal paid)
    {
        var b = Booking(BookingStatus.Confirmed) with { Total = total, Paid = paid };
        Assert.Contains(DataQualityRules.Evaluate([b]), x => x.Rule == "invalid_booking");
    }
}
