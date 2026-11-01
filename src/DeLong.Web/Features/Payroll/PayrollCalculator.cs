using DeLong.Web.Domain.Enums;

namespace DeLong.Web.Features.Payroll;

public sealed record PayrollCalcPolicy(int SalaryDaysBase, decimal AttendanceBonus, decimal HolidayMultiplier, decimal TetMultiplier);

public sealed record PayrollCalcRate(DateOnly EffectiveFrom, decimal MonthlySalary);

public sealed record PayrollCalcEmployee(
    Guid Id,
    int ShiftsPerDay,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool AttendanceBonusEligible,
    IReadOnlyList<PayrollCalcRate> Rates);

public sealed record PayrollCalcInput(
    DateOnly Month,
    PayrollCalcPolicy Policy,
    PayrollCalcEmployee Employee,
    IReadOnlyDictionary<DateOnly, PayrollHolidayKind> Holidays,
    IReadOnlyDictionary<(DateOnly Date, int Slot), AttendanceStatus> Entries,
    IReadOnlyList<decimal> Adjustments,
    decimal Advances,
    decimal Paid);

public sealed record PayrollSlotRef(DateOnly Date, int Slot);

public sealed record PayrollLine(
    Guid EmployeeId,
    decimal MonthlySalary,
    decimal SalaryPerDay,
    decimal SalaryPerShift,
    int DaysInMonth,
    int ScheduledShifts,
    int PresentShifts,
    int UnpaidShifts,
    int PaidLeaveShifts,
    int HolidayOffShifts,
    int MadeUpShifts,
    int HolidayWorkedShifts,
    int BlankShifts,
    decimal BasePay,
    decimal UnpaidDeduction,
    decimal HolidayPremium,
    decimal AttendanceBonus,
    string? AttendanceBonusLostReason,
    decimal Additions,
    decimal Deductions,
    decimal Gross,
    decimal Advances,
    decimal Paid,
    decimal Remaining,
    IReadOnlyList<PayrollSlotRef> UnpaidSlots,
    IReadOnlyList<PayrollSlotRef> HolidayWorkedSlots,
    IReadOnlyList<PayrollSlotRef> BlankSlots);

/// <summary>
/// Quy tắc lương đã chốt với chủ nhà (01/11/2026):
/// - Đơn giá ngày = lương tháng ÷ 30 (cố định); đơn giá ca = đơn giá ngày ÷ số ca/ngày.
/// - Lương cơ bản = đơn giá ca × số ca chuẩn thực có trong tháng (tháng 31 ngày được 31 ngày, tháng 2 theo ngày thực).
/// - Ca nghỉ không lương bị trừ đúng đơn giá ca; nghỉ có lương, nghỉ lễ, nghỉ đã làm bù không bị trừ.
/// - Ca làm ngày lễ/Tết được trả tổng ×hệ số; lương cơ bản đã có ×1 nên phụ trội = đơn giá ca × (hệ số − 1).
/// - Chuyên cần khi làm đủ cả tháng và không có ca nghỉ nào (trừ nghỉ lễ), không còn ô chưa chấm.
/// Mỗi ca tính theo mức lương có hiệu lực vào ngày đó. Hàm thuần, không đọc database.
/// </summary>
public static class PayrollCalculator
{
    public static PayrollLine Calculate(PayrollCalcInput input)
    {
        var policy = input.Policy;
        var employee = input.Employee;
        if (policy.SalaryDaysBase <= 0) throw new ArgumentOutOfRangeException(nameof(input), "Số ngày chia lương phải lớn hơn 0.");
        if (employee.ShiftsPerDay <= 0) throw new ArgumentOutOfRangeException(nameof(input), "Số ca/ngày phải lớn hơn 0.");

        var first = new DateOnly(input.Month.Year, input.Month.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(first.Year, first.Month);
        var last = first.AddDays(daysInMonth - 1);
        var rates = employee.Rates.OrderBy(x => x.EffectiveFrom).ToList();
        var divisor = (decimal)policy.SalaryDaysBase * employee.ShiftsPerDay;

        // Gom theo mức lương để nhân trước rồi chia một lần, tránh lệch làm tròn.
        var baseUnits = new Dictionary<decimal, int>();
        var unpaidUnits = new Dictionary<decimal, int>();
        var premiumUnits = new Dictionary<(decimal Salary, decimal Extra), int>();
        int scheduled = 0, present = 0, unpaid = 0, paidLeave = 0, holidayOff = 0, madeUp = 0, holidayWorked = 0, blank = 0;
        var unpaidSlots = new List<PayrollSlotRef>();
        var holidaySlots = new List<PayrollSlotRef>();
        var blankSlots = new List<PayrollSlotRef>();

        for (var day = first; day <= last; day = day.AddDays(1))
        {
            if (day < employee.StartDate || (employee.EndDate.HasValue && day > employee.EndDate.Value)) continue;
            var salary = SalaryOn(rates, day);
            var isHoliday = input.Holidays.TryGetValue(day, out var holidayKind);
            for (var slot = 0; slot < employee.ShiftsPerDay; slot++)
            {
                scheduled++;
                Add(baseUnits, salary);
                if (!input.Entries.TryGetValue((day, slot), out var status))
                {
                    blank++;
                    blankSlots.Add(new(day, slot));
                    continue;
                }
                switch (status)
                {
                    case AttendanceStatus.Present:
                        present++;
                        if (isHoliday)
                        {
                            holidayWorked++;
                            holidaySlots.Add(new(day, slot));
                            var multiplier = holidayKind == PayrollHolidayKind.Tet ? policy.TetMultiplier : policy.HolidayMultiplier;
                            var extra = Math.Max(0m, multiplier - 1m);
                            if (extra > 0) Add(premiumUnits, (salary, extra));
                        }
                        break;
                    case AttendanceStatus.UnpaidLeave:
                        unpaid++;
                        unpaidSlots.Add(new(day, slot));
                        Add(unpaidUnits, salary);
                        break;
                    case AttendanceStatus.PaidLeave: paidLeave++; break;
                    case AttendanceStatus.HolidayOff: holidayOff++; break;
                    case AttendanceStatus.MadeUp: madeUp++; break;
                }
            }
        }

        var basePay = Money(baseUnits.Sum(x => x.Key * x.Value / divisor));
        var deduction = Money(unpaidUnits.Sum(x => x.Key * x.Value / divisor));
        var premium = Money(premiumUnits.Sum(x => x.Key.Salary * x.Key.Extra * x.Value / divisor));

        var fullMonth = employee.StartDate <= first && (!employee.EndDate.HasValue || employee.EndDate.Value >= last);
        string? lostReason = null;
        if (!employee.AttendanceBonusEligible) lostReason = "Không áp dụng chuyên cần cho nhân viên này";
        else if (!fullMonth) lostReason = "Không làm đủ cả tháng (vào hoặc nghỉ việc trong tháng)";
        else if (unpaid > 0) lostReason = $"Có {unpaid} ca nghỉ không lương";
        else if (paidLeave > 0) lostReason = $"Có {paidLeave} ca nghỉ có lương";
        else if (madeUp > 0) lostReason = $"Có {madeUp} ca nghỉ (đã làm bù)";
        else if (blank > 0) lostReason = $"Còn {blank} ca chưa chấm";
        var bonus = lostReason is null ? Money(policy.AttendanceBonus) : 0m;

        var additions = Money(input.Adjustments.Where(x => x > 0).Sum());
        var deductions = Money(-input.Adjustments.Where(x => x < 0).Sum());
        var gross = basePay - deduction + premium + bonus + additions - deductions;
        var remaining = gross - input.Advances - input.Paid;

        var displaySalary = SalaryOn(rates, last);
        return new PayrollLine(
            employee.Id, displaySalary, Money(displaySalary / policy.SalaryDaysBase), Money(displaySalary / divisor),
            daysInMonth, scheduled, present, unpaid, paidLeave, holidayOff, madeUp, holidayWorked, blank,
            basePay, deduction, premium, bonus, lostReason, additions, deductions, gross,
            input.Advances, input.Paid, remaining, unpaidSlots, holidaySlots, blankSlots);
    }

    public static decimal SalaryOn(IReadOnlyList<PayrollCalcRate> orderedRates, DateOnly day)
    {
        decimal salary = 0;
        foreach (var rate in orderedRates)
        {
            if (rate.EffectiveFrom > day) break;
            salary = rate.MonthlySalary;
        }
        return salary;
    }

    public static decimal Money(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static void Add<T>(Dictionary<T, int> map, T key) where T : notnull =>
        map[key] = map.TryGetValue(key, out var count) ? count + 1 : 1;
}
