using DeLong.Web.Features.Bookings;
using Xunit;
using W = DeLong.Web.Features.Bookings.StaffBookingQuoteService.RateWindow;

namespace DeLong.Tests;

public sealed class StaffBookingQuoteTests
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static readonly Guid Ca1 = Guid.NewGuid(), Ca2 = Guid.NewGuid(), Ca3 = Guid.NewGuid(), Night = Guid.NewGuid();
    private static readonly W[] Windows =
    [
        new(0, Ca1, "Ca 1", new(11, 0), new(14, 0), false),
        new(1, Ca2, "Ca 2", new(14, 0), new(17, 30), false),
        new(2, Ca3, "Ca 3", new(18, 0), new(21, 0), false),
        new(3, Night, "Qua đêm", new(21, 30), new(10, 0), true)
    ];

    private static DateTime At(int day, int hour, int minute = 0) => new DateTimeOffset(2026, 11, day, hour, minute, 0, TimeSpan.FromHours(7)).UtcDateTime;
    private static string[] Pick(Guid? rateId, DateTime from, DateTime to) =>
        StaffBookingQuoteService.PickSlots(Windows, rateId, from, to, Zone).Select(x => $"{x.Window.Name}@{x.Date:dd}").ToArray();

    [Fact]
    public void Checkout_past_the_chosen_ca_adds_the_next_ca() =>
        Assert.Equal(["Ca 1@23", "Ca 2@23"], Pick(Ca1, At(23, 11), At(23, 17, 30)));

    [Fact]
    public void Partly_used_next_ca_is_counted_in_full() =>
        Assert.Equal(["Ca 1@23", "Ca 2@23"], Pick(Ca1, At(23, 10, 30), At(23, 15)));

    [Fact]
    public void Stay_inside_one_ca_stays_one_ca() =>
        Assert.Equal(["Ca 1@23"], Pick(Ca1, At(23, 11), At(23, 14)));

    [Fact]
    public void Checkout_in_the_gap_before_the_next_ca_does_not_add_it() =>
        Assert.Equal(["Ca 1@23", "Ca 2@23"], Pick(Ca1, At(23, 11), At(23, 17, 50)));

    [Fact]
    public void Without_a_chosen_ca_the_check_in_ca_is_the_first() =>
        Assert.Equal(["Ca 2@23", "Ca 3@23"], Pick(null, At(23, 14, 30), At(23, 20)));

    [Fact]
    public void Overnight_continues_into_the_first_ca_of_the_next_day() =>
        Assert.Equal(["Qua đêm@23", "Ca 1@24"], Pick(Night, At(23, 21, 30), At(24, 12)));

    [Fact]
    public void Check_in_after_midnight_finds_the_previous_days_overnight() =>
        Assert.Equal(["Qua đêm@23"], Pick(null, At(24, 1), At(24, 9)));
}
