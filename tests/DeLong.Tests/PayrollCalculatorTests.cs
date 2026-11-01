using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Payroll;
using Xunit;

namespace DeLong.Tests;

public sealed class PayrollCalculatorTests
{
    private static readonly PayrollCalcPolicy Policy = new(30, 300_000m, 2m, 3m);
    private static readonly Dictionary<DateOnly, PayrollHolidayKind> NoHolidays = new();

    private static PayrollCalcEmployee Employee(decimal salary, int shifts = 4, DateOnly? start = null, DateOnly? end = null, params PayrollCalcRate[] extraRates) =>
        new(Guid.NewGuid(), shifts, start ?? new DateOnly(2025, 1, 1), end, true,
            [new PayrollCalcRate(start ?? new DateOnly(2025, 1, 1), salary), .. extraRates]);

    private static Dictionary<(DateOnly, int), AttendanceStatus> AllPresent(DateOnly month, int shifts, DateOnly? from = null)
    {
        var map = new Dictionary<(DateOnly, int), AttendanceStatus>();
        for (var d = month; d.Month == month.Month; d = d.AddDays(1))
        {
            if (from.HasValue && d < from.Value) continue;
            for (var s = 0; s < shifts; s++) map[(d, s)] = AttendanceStatus.Present;
        }
        return map;
    }

    private static PayrollLine Calc(DateOnly month, PayrollCalcEmployee employee, Dictionary<(DateOnly, int), AttendanceStatus> entries,
        Dictionary<DateOnly, PayrollHolidayKind>? holidays = null, decimal[]? adjustments = null, decimal advances = 0, decimal paid = 0, PayrollCalcPolicy? policy = null) =>
        PayrollCalculator.Calculate(new(month, policy ?? Policy, employee, holidays ?? NoHolidays, entries, adjustments ?? [], advances, paid));

    [Fact]
    public void Full_thirty_day_month_without_absence_pays_salary_plus_attendance_bonus()
    {
        var month = new DateOnly(2026, 9, 1);
        var line = Calc(month, Employee(6_000_000m), AllPresent(month, 4));
        Assert.Equal(120, line.ScheduledShifts);
        Assert.Equal(200_000m, line.SalaryPerDay);
        Assert.Equal(50_000m, line.SalaryPerShift);
        Assert.Equal(6_000_000m, line.BasePay);
        Assert.Equal(300_000m, line.AttendanceBonus);
        Assert.Null(line.AttendanceBonusLostReason);
        Assert.Equal(6_300_000m, line.Gross);
    }

    [Theory]
    [InlineData(2026, 10, 6_200_000)] // 31 ngày: được thêm 1 ngày
    [InlineData(2026, 2, 5_600_000)]  // tháng 2 tính theo ngày thực
    [InlineData(2028, 2, 5_800_000)]  // năm nhuận 29 ngày
    public void Base_pay_follows_actual_days_with_fixed_thirty_day_divisor(int year, int monthNumber, int expected)
    {
        var month = new DateOnly(year, monthNumber, 1);
        var line = Calc(month, Employee(6_000_000m), AllPresent(month, 4));
        Assert.Equal(expected, line.BasePay);
    }

    [Fact]
    public void Unpaid_leave_is_deducted_made_up_shift_is_not_and_holiday_work_is_paid_double_in_total()
    {
        var month = new DateOnly(2026, 9, 1);
        var entries = AllPresent(month, 4);
        entries[(new DateOnly(2026, 9, 10), 2)] = AttendanceStatus.UnpaidLeave;
        entries[(new DateOnly(2026, 9, 10), 3)] = AttendanceStatus.MadeUp;
        var holidays = new Dictionary<DateOnly, PayrollHolidayKind> { [new DateOnly(2026, 9, 2)] = PayrollHolidayKind.Holiday };

        var line = Calc(month, Employee(6_000_000m), entries, holidays);

        Assert.Equal(1, line.UnpaidShifts);
        Assert.Equal(1, line.MadeUpShifts);
        Assert.Equal(50_000m, line.UnpaidDeduction);
        Assert.Equal(4, line.HolidayWorkedShifts);
        Assert.Equal(200_000m, line.HolidayPremium); // 4 ca × 50.000 × (2 − 1)
        Assert.Equal(0m, line.AttendanceBonus);
        Assert.Equal(6_150_000m, line.Gross);
        Assert.Single(line.UnpaidSlots);
    }

    [Fact]
    public void Tet_shift_is_paid_triple_in_total_and_holiday_off_keeps_attendance_bonus()
    {
        var month = new DateOnly(2026, 2, 1);
        var entries = AllPresent(month, 4);
        var tet = new DateOnly(2026, 2, 17);
        var off = new DateOnly(2026, 2, 18);
        for (var s = 0; s < 4; s++) entries[(off, s)] = AttendanceStatus.HolidayOff;
        var holidays = new Dictionary<DateOnly, PayrollHolidayKind> { [tet] = PayrollHolidayKind.Tet, [off] = PayrollHolidayKind.Tet };

        var line = Calc(month, Employee(6_000_000m), entries, holidays);

        Assert.Equal(4, line.HolidayWorkedShifts);
        Assert.Equal(400_000m, line.HolidayPremium); // 4 ca × 50.000 × (3 − 1)
        Assert.Equal(4, line.HolidayOffShifts);
        Assert.Equal(300_000m, line.AttendanceBonus);
        Assert.Equal(5_600_000m + 400_000m + 300_000m, line.Gross);
    }

    [Fact]
    public void Paid_leave_is_not_deducted_but_loses_attendance_bonus()
    {
        var month = new DateOnly(2026, 9, 1);
        var entries = AllPresent(month, 4);
        entries[(new DateOnly(2026, 9, 15), 0)] = AttendanceStatus.PaidLeave;
        var line = Calc(month, Employee(6_000_000m), entries);
        Assert.Equal(0m, line.UnpaidDeduction);
        Assert.Equal(0m, line.AttendanceBonus);
        Assert.Equal(6_000_000m, line.Gross);
    }

    [Fact]
    public void Blank_cells_are_not_treated_as_absence_but_block_the_bonus()
    {
        var month = new DateOnly(2026, 9, 1);
        var entries = AllPresent(month, 4);
        entries.Remove((new DateOnly(2026, 9, 30), 0));
        entries.Remove((new DateOnly(2026, 9, 30), 1));
        var line = Calc(month, Employee(6_000_000m), entries);
        Assert.Equal(2, line.BlankShifts);
        Assert.Equal(0m, line.UnpaidDeduction);
        Assert.Equal(6_000_000m, line.BasePay);
        Assert.Equal(0m, line.AttendanceBonus);
        Assert.Equal(2, line.BlankSlots.Count);
    }

    [Fact]
    public void Joining_mid_month_counts_only_employed_days_and_gets_no_bonus()
    {
        var month = new DateOnly(2026, 9, 1);
        var start = new DateOnly(2026, 9, 16);
        var line = Calc(month, Employee(6_000_000m, start: start), AllPresent(month, 4, start));
        Assert.Equal(60, line.ScheduledShifts);
        Assert.Equal(3_000_000m, line.BasePay);
        Assert.Equal(0m, line.AttendanceBonus);
        Assert.Equal(0, line.BlankShifts);
    }

    [Fact]
    public void Leaving_mid_month_stops_counting_after_end_date()
    {
        var month = new DateOnly(2026, 9, 1);
        var line = Calc(month, Employee(6_000_000m, end: new DateOnly(2026, 9, 10)), AllPresent(month, 4));
        Assert.Equal(40, line.ScheduledShifts);
        Assert.Equal(2_000_000m, line.BasePay);
        Assert.Equal(0m, line.AttendanceBonus);
    }

    [Fact]
    public void Salary_change_mid_month_values_each_shift_at_the_rate_in_effect_that_day()
    {
        var month = new DateOnly(2026, 9, 1);
        var employee = Employee(6_000_000m, extraRates: new PayrollCalcRate(new DateOnly(2026, 9, 16), 9_000_000m));
        var line = Calc(month, employee, AllPresent(month, 4));
        Assert.Equal(7_500_000m, line.BasePay); // 15 ngày × 200.000 + 15 ngày × 300.000
        Assert.Equal(9_000_000m, line.MonthlySalary);
    }

    [Fact]
    public void Non_divisible_salary_rounds_each_money_line_once_to_whole_dong()
    {
        var month = new DateOnly(2026, 10, 1);
        var line = Calc(month, Employee(6_500_000m), AllPresent(month, 4));
        Assert.Equal(6_716_667m, line.BasePay); // 6.500.000 × 31 ÷ 30
        var september = new DateOnly(2026, 9, 1);
        Assert.Equal(6_500_000m, Calc(september, Employee(6_500_000m), AllPresent(september, 4)).BasePay);
    }

    [Fact]
    public void Two_shift_employee_uses_own_shift_count()
    {
        var month = new DateOnly(2026, 9, 1);
        var line = Calc(month, Employee(4_800_000m, shifts: 2), AllPresent(month, 2));
        Assert.Equal(60, line.ScheduledShifts);
        Assert.Equal(80_000m, line.SalaryPerShift);
        Assert.Equal(4_800_000m, line.BasePay);
    }

    [Fact]
    public void Adjustments_advances_and_payments_flow_into_remaining()
    {
        var month = new DateOnly(2026, 9, 1);
        var line = Calc(month, Employee(6_000_000m), AllPresent(month, 4), adjustments: [200_000m, 150_000m, -100_000m], advances: 1_000_000m, paid: 2_000_000m);
        Assert.Equal(350_000m, line.Additions);
        Assert.Equal(100_000m, line.Deductions);
        Assert.Equal(6_550_000m, line.Gross);
        Assert.Equal(3_550_000m, line.Remaining);
    }

    [Fact]
    public void Advance_larger_than_gross_gives_negative_remaining()
    {
        var month = new DateOnly(2026, 9, 1);
        var line = Calc(month, Employee(1_000_000m), AllPresent(month, 4), advances: 2_000_000m);
        Assert.True(line.Remaining < 0);
    }
}
