using System.Globalization;
using System.Text.Json;
using DeLong.Web.Common.Auditing;
using DeLong.Web.Common.Persistence;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeLong.Web.Features.Payroll;

public sealed class PayrollService(AppDbContext db, AuditService audit)
{
    public const string ExpenseSourceType = "Payroll";
    public const string SalaryExpenseCategory = "Lương nhân viên";
    public const string AdvanceExpenseCategory = "Ứng lương";
    private const int MaxCellsPerRequest = 6000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool TryParseMonth(string? value, out DateOnly month)
    {
        month = default;
        if (string.IsNullOrWhiteSpace(value) ||
            !DateOnly.TryParseExact(value.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;
        if (parsed.Year < 2000 || parsed.Year > 2100) return false;
        month = parsed;
        return true;
    }

    private static string MonthText(DateOnly month) => month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    private static DateOnly LastDay(DateOnly month) => month.AddMonths(1).AddDays(-1);

    // ---------------- Đọc ----------------

    public async Task<(PayrollSheetDto? Sheet, PayrollError? Error)> GetSheetAsync(
        Guid propertyId, string? monthValue, bool canManagePayroll, CancellationToken ct)
    {
        if (!TryParseMonth(monthValue, out var month)) return (null, new("validation", "Tháng không hợp lệ (định dạng yyyy-MM)."));
        var last = LastDay(month);

        var employees = await db.Employees.AsNoTracking()
            .Where(x => x.PropertyId == propertyId)
            .Include(x => x.SalaryRates)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .ToListAsync(ct);
        var attendance = await db.AttendanceEntries.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.WorkDate >= month && x.WorkDate <= last)
            .Select(x => new AttendanceCellDto(x.EmployeeId, x.WorkDate, x.Slot, x.Status, x.Note))
            .ToListAsync(ct);

        var holidays = await db.PayrollHolidays.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Date.Year == month.Year)
            .OrderBy(x => x.Date)
            .Select(x => new PayrollHolidayDto(x.Id, x.Date, x.Name, x.Kind))
            .ToListAsync(ct);

        var period = await db.PayrollPeriods.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Month == month, ct);
        string? lockedByName = null;
        if (period?.LockedByUserId is Guid lockedBy)
            lockedByName = await db.Users.AsNoTracking().Where(x => x.Id == lockedBy).Select(x => x.DisplayName).SingleOrDefaultAsync(ct);
        var periodDto = new PayrollPeriodDto(period?.Status ?? PayrollPeriodStatus.Draft, period?.LockedAtUtc, lockedByName);

        var employeeDtos = employees.Select(e =>
        {
            var rates = e.SalaryRates.OrderBy(r => r.EffectiveFrom).ToList();
            return new PayrollEmployeeDto(e.Id, e.Code, e.FullName, e.Position, e.Phone, e.ShiftsPerDay, e.StartDate, e.EndDate,
                e.AttendanceBonusEligible, e.IsActive, e.SortOrder, e.Note,
                canManagePayroll ? PayrollCalculator.SalaryOn(rates.Select(r => new PayrollCalcRate(r.EffectiveFrom, r.MonthlySalary)).ToList(), last) : null,
                canManagePayroll ? rates.Select(r => new PayrollSalaryRateDto(r.Id, r.EffectiveFrom, r.MonthlySalary)).ToList() : null);
        }).ToList();

        if (!canManagePayroll)
        {
            return (new PayrollSheetDto(MonthText(month), DateTime.DaysInMonth(month.Year, month.Month), false, periodDto,
                holidays, employeeDtos, attendance, null, null, null, null), null);
        }

        var policy = await GetPolicyAsync(propertyId, ct);
        var adjustments = await db.PayrollAdjustments.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Month == month)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new PayrollAdjustmentDto(x.Id, x.EmployeeId, x.Type, x.Amount, x.Reason, x.CreatedAtUtc))
            .ToListAsync(ct);
        var transactions = await db.PayrollTransactions.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && x.Month == month)
            .OrderBy(x => x.OccurredAtUtc)
            .Select(x => new PayrollTransactionDto(x.Id, x.EmployeeId, x.Kind, x.Amount, x.Method, x.OccurredAtUtc, x.Note, x.ExpenseId, x.IsVoided, x.VoidReason))
            .ToListAsync(ct);

        IReadOnlyList<PayrollLine> lines;
        if (period is { Status: PayrollPeriodStatus.Locked, SnapshotJson: not null })
        {
            var snapshot = JsonSerializer.Deserialize<List<PayrollLine>>(period.SnapshotJson, JsonOptions) ?? [];
            lines = snapshot.Select(line => WithPayments(line, transactions)).ToList();
        }
        else
        {
            lines = Calculate(month, ToCalcPolicy(policy), employees.Where(e => Overlaps(e, month)).ToList(), holidays, attendance, adjustments, transactions);
        }

        return (new PayrollSheetDto(MonthText(month), DateTime.DaysInMonth(month.Year, month.Month), true, periodDto,
            holidays, employeeDtos, attendance,
            new PayrollPolicyDto(policy.SalaryDaysBase, policy.AttendanceBonus, policy.HolidayMultiplier, policy.TetMultiplier),
            lines, adjustments, transactions), null);
    }

    private static bool Overlaps(Employee e, DateOnly month) =>
        e.StartDate <= LastDay(month) && (!e.EndDate.HasValue || e.EndDate.Value >= month);

    private static PayrollCalcPolicy ToCalcPolicy(PayrollPolicy p) =>
        new(p.SalaryDaysBase, p.AttendanceBonus, p.HolidayMultiplier, p.TetMultiplier);

    private static PayrollLine WithPayments(PayrollLine line, IReadOnlyList<PayrollTransactionDto> transactions)
    {
        var mine = transactions.Where(t => t.EmployeeId == line.EmployeeId && !t.IsVoided).ToList();
        var advances = mine.Where(t => t.Kind == PayrollTransactionKind.Advance).Sum(t => t.Amount);
        var paid = mine.Where(t => t.Kind == PayrollTransactionKind.Payment).Sum(t => t.Amount);
        return line with { Advances = advances, Paid = paid, Remaining = line.Gross - advances - paid };
    }

    private static List<PayrollLine> Calculate(DateOnly month, PayrollCalcPolicy policy, IReadOnlyList<Employee> employees,
        IReadOnlyList<PayrollHolidayDto> holidays, IReadOnlyList<AttendanceCellDto> attendance,
        IReadOnlyList<PayrollAdjustmentDto> adjustments, IReadOnlyList<PayrollTransactionDto> transactions)
    {
        var holidayMap = holidays.GroupBy(h => h.Date).ToDictionary(g => g.Key, g => g.Any(x => x.Kind == PayrollHolidayKind.Tet) ? PayrollHolidayKind.Tet : PayrollHolidayKind.Holiday);
        var byEmployee = attendance.GroupBy(a => a.EmployeeId).ToDictionary(g => g.Key, g => g.ToDictionary(a => (a.Date, a.Slot), a => a.Status));
        return employees.Select(e =>
        {
            var line = PayrollCalculator.Calculate(new PayrollCalcInput(
                month, policy,
                new PayrollCalcEmployee(e.Id, e.ShiftsPerDay, e.StartDate, e.EndDate, e.AttendanceBonusEligible,
                    e.SalaryRates.Select(r => new PayrollCalcRate(r.EffectiveFrom, r.MonthlySalary)).ToList()),
                holidayMap,
                byEmployee.TryGetValue(e.Id, out var entries) ? entries : new Dictionary<(DateOnly, int), AttendanceStatus>(),
                adjustments.Where(a => a.EmployeeId == e.Id).Select(a => a.Amount).ToList(),
                0m, 0m));
            return WithPayments(line, transactions);
        }).ToList();
    }

    private async Task<PayrollPolicy> GetPolicyAsync(Guid propertyId, CancellationToken ct) =>
        await db.PayrollPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct)
        ?? new PayrollPolicy { PropertyId = propertyId };

    private async Task<bool> IsLockedAsync(Guid propertyId, DateOnly month, CancellationToken ct) =>
        await db.PayrollPeriods.AnyAsync(x => x.PropertyId == propertyId && x.Month == month && x.Status == PayrollPeriodStatus.Locked, ct);

    private static Task LockMonthAsync(AppDbContext db, Guid propertyId, DateOnly month, CancellationToken ct) =>
        RequestFingerprint.LockAsync(db, $"payroll:{propertyId}:{MonthText(month)}", ct);

    // ---------------- Chấm công ----------------

    public async Task<PayrollError?> SaveAttendanceAsync(Guid propertyId, SaveAttendanceRequest request, Guid? actorUserId, CancellationToken ct)
    {
        if (!TryParseMonth(request.Month, out var month)) return new("validation", "Tháng không hợp lệ.");
        if (request.Cells is null || request.Cells.Count == 0) return null;
        if (request.Cells.Count > MaxCellsPerRequest) return new("validation", $"Tối đa {MaxCellsPerRequest} ô mỗi lần lưu.");
        var last = LastDay(month);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMonthAsync(db, propertyId, month, ct);
        if (await IsLockedAsync(propertyId, month, ct)) return new("locked", "Bảng lương tháng này đã chốt, không sửa được chấm công.");

        var employeeIds = request.Cells.Select(c => c.EmployeeId).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(x => x.PropertyId == propertyId && employeeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        if (employees.Count != employeeIds.Count) return new("validation", "Có nhân viên không thuộc cơ sở này.");

        foreach (var cell in request.Cells)
        {
            var e = employees[cell.EmployeeId];
            if (cell.Date < month || cell.Date > last) return new("validation", "Ngày chấm công nằm ngoài tháng đang chọn.");
            if (cell.Slot < 0 || cell.Slot >= e.ShiftsPerDay) return new("validation", $"{e.Code} chỉ có {e.ShiftsPerDay} ca/ngày.");
            if (cell.Date < e.StartDate || (e.EndDate.HasValue && cell.Date > e.EndDate.Value))
                return new("validation", $"Ngày {cell.Date:dd/MM/yyyy} nằm ngoài thời gian làm việc của {e.Code}.");
            if (cell.Status.HasValue && !Enum.IsDefined(cell.Status.Value)) return new("validation", "Trạng thái chấm công không hợp lệ.");
            if (cell.Note?.Length > 500) return new("validation", "Ghi chú tối đa 500 ký tự.");
        }

        var existing = await db.AttendanceEntries
            .Where(x => x.PropertyId == propertyId && employeeIds.Contains(x.EmployeeId) && x.WorkDate >= month && x.WorkDate <= last)
            .ToListAsync(ct);
        var map = existing.ToDictionary(x => (x.EmployeeId, x.WorkDate, x.Slot));
        var changed = new Dictionary<Guid, int>();

        // Ô cuối cùng trong yêu cầu thắng nếu một ô bị gửi nhiều lần.
        foreach (var cell in request.Cells.GroupBy(c => (c.EmployeeId, c.Date, c.Slot)).Select(g => g.Last()))
        {
            var key = (cell.EmployeeId, cell.Date, cell.Slot);
            var note = string.IsNullOrWhiteSpace(cell.Note) ? null : cell.Note.Trim();
            if (map.TryGetValue(key, out var row))
            {
                if (cell.Status is null) { db.AttendanceEntries.Remove(row); map.Remove(key); }
                else if (row.Status != cell.Status.Value || row.Note != note) { row.Status = cell.Status.Value; row.Note = note; row.UpdatedByUserId = actorUserId; }
                else continue;
            }
            else
            {
                if (cell.Status is null) continue;
                var added = new AttendanceEntry
                {
                    PropertyId = propertyId, EmployeeId = cell.EmployeeId, WorkDate = cell.Date, Slot = cell.Slot,
                    Status = cell.Status.Value, Note = note, UpdatedByUserId = actorUserId
                };
                db.AttendanceEntries.Add(added);
                map[key] = added;
            }
            changed[cell.EmployeeId] = changed.TryGetValue(cell.EmployeeId, out var n) ? n + 1 : 1;
        }

        foreach (var (employeeId, count) in changed)
            audit.Add(propertyId, PayrollAuditTypes.Attendance, employeeId, "Updated", actorUserId, after: new { month = MonthText(month), cells = count });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    // ---------------- Nhân viên ----------------

    public async Task<(Guid? Id, PayrollError? Error)> SaveEmployeeAsync(Guid propertyId, Guid? employeeId, SaveEmployeeRequest request, Guid? actorUserId, CancellationToken ct)
    {
        var code = request.Code?.Trim() ?? string.Empty;
        var name = request.FullName?.Trim() ?? string.Empty;
        if (code.Length is 0 or > 20) return (null, new("validation", "Mã nhân viên bắt buộc, tối đa 20 ký tự."));
        if (name.Length is 0 or > 200) return (null, new("validation", "Họ tên bắt buộc, tối đa 200 ký tự."));
        if (request.ShiftsPerDay is < 1 or > 6) return (null, new("validation", "Số ca/ngày từ 1 đến 6."));
        if (request.MonthlySalary < 0 || request.MonthlySalary > 10_000_000_000m || decimal.Round(request.MonthlySalary, 0) != request.MonthlySalary)
            return (null, new("validation", "Lương tháng phải là số đồng không âm."));
        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate) return (null, new("validation", "Ngày nghỉ việc phải sau ngày vào làm."));
        if (request.Position?.Length > 100 || request.Phone?.Length > 30 || request.Note?.Length > 2000) return (null, new("validation", "Nội dung vượt giới hạn."));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RequestFingerprint.LockAsync(db, $"payroll-employee:{propertyId}", ct);
        if (await db.Employees.AnyAsync(x => x.PropertyId == propertyId && x.Code == code && x.Id != employeeId, ct))
            return (null, new("duplicate", $"Mã {code} đã được dùng."));

        Employee employee;
        if (employeeId is null)
        {
            employee = new Employee { PropertyId = propertyId };
            db.Employees.Add(employee);
        }
        else
        {
            var found = await db.Employees.Include(x => x.SalaryRates).SingleOrDefaultAsync(x => x.Id == employeeId && x.PropertyId == propertyId, ct);
            if (found is null) return (null, new("not_found", "Không tìm thấy nhân viên."));
            employee = found;
        }

        var before = employeeId is null ? null : new { employee.Code, employee.FullName, employee.ShiftsPerDay, employee.StartDate, employee.EndDate, employee.IsActive };
        employee.Code = code;
        employee.FullName = name;
        employee.Position = Clean(request.Position);
        employee.Phone = Clean(request.Phone);
        employee.ShiftsPerDay = request.ShiftsPerDay;
        employee.StartDate = request.StartDate;
        employee.EndDate = request.EndDate;
        employee.AttendanceBonusEligible = request.AttendanceBonusEligible;
        employee.IsActive = request.IsActive;
        employee.SortOrder = request.SortOrder;
        employee.Note = Clean(request.Note);

        // Lương: nếu khác mức hiện hành thì thêm/sửa mức có hiệu lực từ ngày được chọn.
        var rates = employee.SalaryRates.OrderBy(x => x.EffectiveFrom).ToList();
        var effectiveFrom = request.SalaryEffectiveFrom ?? (rates.Count == 0 ? request.StartDate : DateOnly.FromDateTime(DateTime.UtcNow));
        if (rates.Count == 0) effectiveFrom = request.StartDate < effectiveFrom ? request.StartDate : effectiveFrom;
        var current = PayrollCalculator.SalaryOn(rates.Select(r => new PayrollCalcRate(r.EffectiveFrom, r.MonthlySalary)).ToList(), effectiveFrom);
        if (rates.Count == 0 || current != request.MonthlySalary)
        {
            var effectiveMonth = new DateOnly(effectiveFrom.Year, effectiveFrom.Month, 1);
            if (rates.Count > 0 && await db.PayrollPeriods.AnyAsync(x => x.PropertyId == propertyId && x.Month >= effectiveMonth && x.Status == PayrollPeriodStatus.Locked, ct))
                return (null, new("locked", "Ngày hiệu lực lương rơi vào tháng đã chốt. Chọn ngày thuộc tháng chưa chốt."));
            var sameDay = rates.SingleOrDefault(x => x.EffectiveFrom == effectiveFrom);
            if (sameDay is not null) sameDay.MonthlySalary = request.MonthlySalary;
            else db.EmployeeSalaryRates.Add(new EmployeeSalaryRate { Employee = employee, EffectiveFrom = effectiveFrom, MonthlySalary = request.MonthlySalary, CreatedByUserId = actorUserId });
        }

        audit.Add(propertyId, PayrollAuditTypes.Employee, employee.Id, employeeId is null ? "Created" : "Updated", actorUserId,
            before, new { employee.Code, employee.FullName, employee.ShiftsPerDay, employee.StartDate, employee.EndDate, employee.IsActive, request.MonthlySalary, salaryEffectiveFrom = effectiveFrom });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (employee.Id, null);
    }

    // ---------------- Tăng / giảm ----------------

    public async Task<(Guid? Id, PayrollError? Error)> SaveAdjustmentAsync(Guid propertyId, Guid? adjustmentId, SaveAdjustmentRequest request, Guid? actorUserId, CancellationToken ct)
    {
        if (!TryParseMonth(request.Month, out var month)) return (null, new("validation", "Tháng không hợp lệ."));
        var type = request.Type?.Trim() ?? string.Empty;
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (type.Length is 0 or > 50) return (null, new("validation", "Loại khoản bắt buộc, tối đa 50 ký tự."));
        if (reason.Length is 0 or > 500) return (null, new("validation", "Lý do bắt buộc, tối đa 500 ký tự."));
        if (request.Amount == 0 || Math.Abs(request.Amount) > 10_000_000_000m || decimal.Round(request.Amount, 0) != request.Amount)
            return (null, new("validation", "Số tiền phải khác 0 và là số đồng."));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMonthAsync(db, propertyId, month, ct);
        if (await IsLockedAsync(propertyId, month, ct)) return (null, new("locked", "Bảng lương tháng này đã chốt."));
        if (!await db.Employees.AnyAsync(x => x.Id == request.EmployeeId && x.PropertyId == propertyId, ct))
            return (null, new("validation", "Nhân viên không thuộc cơ sở này."));

        PayrollAdjustment row;
        if (adjustmentId is null)
        {
            row = new PayrollAdjustment { PropertyId = propertyId, Month = month, CreatedByUserId = actorUserId };
            db.PayrollAdjustments.Add(row);
        }
        else
        {
            var found = await db.PayrollAdjustments.SingleOrDefaultAsync(x => x.Id == adjustmentId && x.PropertyId == propertyId, ct);
            if (found is null) return (null, new("not_found", "Không tìm thấy khoản tăng/giảm."));
            if (found.Month != month) return (null, new("validation", "Không đổi tháng của khoản đã nhập."));
            row = found;
        }
        row.EmployeeId = request.EmployeeId;
        row.Type = type;
        row.Amount = request.Amount;
        row.Reason = reason;
        audit.Add(propertyId, PayrollAuditTypes.PayrollAdjustment, row.Id, adjustmentId is null ? "Created" : "Updated", actorUserId,
            after: new { month = MonthText(month), row.EmployeeId, row.Type, row.Amount, row.Reason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (row.Id, null);
    }

    public async Task<PayrollError?> DeleteAdjustmentAsync(Guid propertyId, Guid adjustmentId, Guid? actorUserId, CancellationToken ct)
    {
        var row = await db.PayrollAdjustments.SingleOrDefaultAsync(x => x.Id == adjustmentId && x.PropertyId == propertyId, ct);
        if (row is null) return new("not_found", "Không tìm thấy khoản tăng/giảm.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMonthAsync(db, propertyId, row.Month, ct);
        if (await IsLockedAsync(propertyId, row.Month, ct)) return new("locked", "Bảng lương tháng này đã chốt.");
        db.PayrollAdjustments.Remove(row);
        audit.Add(propertyId, PayrollAuditTypes.PayrollAdjustment, row.Id, "Deleted", actorUserId,
            before: new { month = MonthText(row.Month), row.EmployeeId, row.Type, row.Amount, row.Reason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    // ---------------- Chính sách & ngày lễ ----------------

    public async Task<PayrollError?> SavePolicyAsync(Guid propertyId, PayrollPolicyDto request, Guid? actorUserId, CancellationToken ct)
    {
        if (request.SalaryDaysBase is < 1 or > 31) return new("validation", "Số ngày chia lương từ 1 đến 31.");
        if (request.AttendanceBonus < 0 || request.AttendanceBonus > 1_000_000_000m) return new("validation", "Tiền chuyên cần không hợp lệ.");
        if (request.HolidayMultiplier is < 1 or > 10 || request.TetMultiplier is < 1 or > 10) return new("validation", "Hệ số lễ/Tết từ 1 đến 10.");

        var policy = await db.PayrollPolicies.SingleOrDefaultAsync(x => x.PropertyId == propertyId, ct);
        if (policy is null) { policy = new PayrollPolicy { PropertyId = propertyId }; db.PayrollPolicies.Add(policy); }
        policy.SalaryDaysBase = request.SalaryDaysBase;
        policy.AttendanceBonus = request.AttendanceBonus;
        policy.HolidayMultiplier = request.HolidayMultiplier;
        policy.TetMultiplier = request.TetMultiplier;
        audit.Add(propertyId, PayrollAuditTypes.PayrollPolicy, policy.Id, "Updated", actorUserId, after: request);
        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<(Guid? Id, PayrollError? Error)> AddHolidayAsync(Guid propertyId, SaveHolidayRequest request, Guid? actorUserId, CancellationToken ct)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > 120) return (null, new("validation", "Tên ngày lễ bắt buộc, tối đa 120 ký tự."));
        if (!Enum.IsDefined(request.Kind)) return (null, new("validation", "Loại ngày không hợp lệ."));
        var month = new DateOnly(request.Date.Year, request.Date.Month, 1);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMonthAsync(db, propertyId, month, ct);
        if (await IsLockedAsync(propertyId, month, ct)) return (null, new("locked", "Tháng này đã chốt lương."));
        if (await db.PayrollHolidays.AnyAsync(x => x.PropertyId == propertyId && x.Date == request.Date, ct))
            return (null, new("duplicate", "Ngày này đã có trong danh sách."));
        var row = new PayrollHoliday { PropertyId = propertyId, Date = request.Date, Name = name, Kind = request.Kind };
        db.PayrollHolidays.Add(row);
        audit.Add(propertyId, PayrollAuditTypes.PayrollHoliday, row.Id, "Created", actorUserId, after: new { row.Date, row.Name, row.Kind });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (row.Id, null);
    }

    public async Task<PayrollError?> DeleteHolidayAsync(Guid propertyId, Guid holidayId, Guid? actorUserId, CancellationToken ct)
    {
        var row = await db.PayrollHolidays.SingleOrDefaultAsync(x => x.Id == holidayId && x.PropertyId == propertyId, ct);
        if (row is null) return new("not_found", "Không tìm thấy ngày lễ.");
        var month = new DateOnly(row.Date.Year, row.Date.Month, 1);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMonthAsync(db, propertyId, month, ct);
        if (await IsLockedAsync(propertyId, month, ct)) return new("locked", "Tháng này đã chốt lương.");
        db.PayrollHolidays.Remove(row);
        audit.Add(propertyId, PayrollAuditTypes.PayrollHoliday, row.Id, "Deleted", actorUserId, before: new { row.Date, row.Name, row.Kind });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    // ---------------- Chốt / mở chốt ----------------

    public async Task<PayrollError?> LockAsync(Guid propertyId, string monthValue, Guid? actorUserId, CancellationToken ct)
    {
        if (!TryParseMonth(monthValue, out var month)) return new("validation", "Tháng không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMonthAsync(db, propertyId, month, ct);
        var period = await db.PayrollPeriods.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Month == month, ct);
        if (period?.Status == PayrollPeriodStatus.Locked) return new("locked", "Bảng lương tháng này đã chốt.");

        var (sheet, error) = await GetSheetAsync(propertyId, monthValue, true, ct);
        if (error is not null) return error;
        var blanks = sheet!.Lines!.Sum(x => x.BlankShifts);
        if (blanks > 0) return new("incomplete", $"Còn {blanks} ca chưa chấm. Chấm đủ rồi mới chốt được.");
        if (sheet.Lines!.Count == 0) return new("validation", "Tháng này chưa có nhân viên nào.");

        if (period is null) { period = new PayrollPeriod { PropertyId = propertyId, Month = month }; db.PayrollPeriods.Add(period); }
        period.Status = PayrollPeriodStatus.Locked;
        period.SnapshotJson = JsonSerializer.Serialize(sheet.Lines, JsonOptions);
        period.LockedAtUtc = DateTime.UtcNow;
        period.LockedByUserId = actorUserId;
        audit.Add(propertyId, PayrollAuditTypes.PayrollPeriod, period.Id, "Locked", actorUserId,
            after: new { month = MonthText(month), employees = sheet.Lines.Count, gross = sheet.Lines.Sum(x => x.Gross) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    public async Task<PayrollError?> UnlockAsync(Guid propertyId, UnlockPayrollRequest request, Guid? actorUserId, CancellationToken ct)
    {
        if (!TryParseMonth(request.Month, out var month)) return new("validation", "Tháng không hợp lệ.");
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 1000) return new("validation", "Cần nhập lý do mở chốt.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMonthAsync(db, propertyId, month, ct);
        var period = await db.PayrollPeriods.SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.Month == month, ct);
        if (period is null || period.Status != PayrollPeriodStatus.Locked) return new("not_locked", "Bảng lương tháng này chưa chốt.");
        if (await db.PayrollTransactions.AnyAsync(x => x.PropertyId == propertyId && x.Month == month && x.Kind == PayrollTransactionKind.Payment && !x.IsVoided, ct))
            return new("has_payments", "Đã có khoản trả lương cho tháng này. Huỷ các khoản trả trước khi mở chốt.");
        period.Status = PayrollPeriodStatus.Draft;
        period.SnapshotJson = null;
        period.LockedAtUtc = null;
        period.LockedByUserId = null;
        audit.Add(propertyId, PayrollAuditTypes.PayrollPeriod, period.Id, "Unlocked", actorUserId, after: new { month = MonthText(month), reason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    // ---------------- Ứng / trả lương ----------------

    public async Task<(PayrollTransactionDto? Result, PayrollError? Error)> CreateTransactionAsync(
        Guid propertyId, CreatePayrollTransactionRequest request, Guid? actorUserId, CancellationToken ct)
    {
        if (request.RequestKey == Guid.Empty) return (null, new("validation", "Mã gửi lại không hợp lệ."));
        if (!TryParseMonth(request.Month, out var month)) return (null, new("validation", "Tháng không hợp lệ."));
        if (!Enum.IsDefined(request.Kind) || !Enum.IsDefined(request.Method)) return (null, new("validation", "Loại hoặc hình thức chi không hợp lệ."));
        if (request.Amount <= 0 || request.Amount > 10_000_000_000m || decimal.Round(request.Amount, 0) != request.Amount)
            return (null, new("validation", "Số tiền phải lớn hơn 0 và là số đồng."));
        if (request.Note?.Length > 500) return (null, new("validation", "Ghi chú tối đa 500 ký tự."));

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMonthAsync(db, propertyId, month, ct);
        var hash = RequestFingerprint.Create(request);
        var previous = await db.PayrollTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.PropertyId == propertyId && x.RequestKey == request.RequestKey, ct);
        if (previous is not null)
            return previous.RequestHash == hash ? (ToDto(previous), null) : (null, new("retry_conflict", "Mã gửi lại đã dùng cho nội dung khác."));

        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.EmployeeId && x.PropertyId == propertyId, ct);
        if (employee is null) return (null, new("validation", "Nhân viên không thuộc cơ sở này."));
        var locked = await IsLockedAsync(propertyId, month, ct);
        if (request.Kind == PayrollTransactionKind.Advance && locked)
            return (null, new("locked", "Tháng này đã chốt; ứng lương ghi vào tháng sau."));
        if (request.Kind == PayrollTransactionKind.Payment)
        {
            if (!locked) return (null, new("not_locked", "Chốt bảng lương trước khi trả lương."));
            var (sheet, _) = await GetSheetAsync(propertyId, request.Month, true, ct);
            var line = sheet?.Lines?.SingleOrDefault(x => x.EmployeeId == request.EmployeeId);
            if (line is null) return (null, new("validation", "Nhân viên không có trong bảng lương tháng này."));
            if (request.Amount > line.Remaining) return (null, new("overpay", $"Số tiền vượt số còn phải trả ({line.Remaining:N0} đ)."));
        }

        var transaction = new PayrollTransaction
        {
            PropertyId = propertyId, EmployeeId = request.EmployeeId, Month = month, Kind = request.Kind, Amount = request.Amount,
            Method = request.Method, OccurredAtUtc = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow, Note = Clean(request.Note),
            RequestKey = request.RequestKey, RequestHash = hash, RecordedByUserId = actorUserId
        };
        var isAdvance = request.Kind == PayrollTransactionKind.Advance;
        // Phiếu chi không ghi tên nhân viên vì danh sách chi phí mở cho Manager/Viewer.
        var expense = new Expense
        {
            PropertyId = propertyId,
            OccurredAtUtc = transaction.OccurredAtUtc,
            Category = isAdvance ? AdvanceExpenseCategory : SalaryExpenseCategory,
            Description = $"{(isAdvance ? "Ứng lương" : "Trả lương")} tháng {month:MM/yyyy}",
            Amount = request.Amount,
            Method = request.Method,
            RecordedByUserId = actorUserId,
            SourceType = ExpenseSourceType,
            SourceId = transaction.Id
        };
        transaction.ExpenseId = expense.Id;
        db.Expenses.Add(expense);
        db.PayrollTransactions.Add(transaction);
        audit.Add(propertyId, "Expense", expense.Id, "Created", actorUserId,
            after: new { expense.OccurredAtUtc, expense.Amount, expense.Method, expense.Category, expense.Description, expense.SourceType });
        audit.Add(propertyId, PayrollAuditTypes.PayrollTransaction, transaction.Id, "Created", actorUserId,
            after: new { month = MonthText(month), transaction.EmployeeId, transaction.Kind, transaction.Amount, transaction.Method, transaction.ExpenseId });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return (ToDto(transaction), null);
    }

    public async Task<PayrollError?> VoidTransactionAsync(Guid propertyId, Guid transactionId, string reason, Guid? actorUserId, CancellationToken ct)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 1000) return new("validation", "Cần nhập lý do huỷ.");
        var row = await db.PayrollTransactions.SingleOrDefaultAsync(x => x.Id == transactionId && x.PropertyId == propertyId, ct);
        if (row is null) return new("not_found", "Không tìm thấy khoản ứng/trả.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockMonthAsync(db, propertyId, row.Month, ct);
        await db.Entry(row).ReloadAsync(ct);
        if (row.IsVoided) return null;
        if (row.Kind == PayrollTransactionKind.Advance && await IsLockedAsync(propertyId, row.Month, ct))
            return new("locked", "Khoản ứng đã được trừ vào bảng lương đã chốt. Mở chốt trước khi huỷ.");
        var expense = await db.Expenses.SingleAsync(x => x.Id == row.ExpenseId, ct);
        var now = DateTime.UtcNow;
        row.IsVoided = true; row.VoidedAtUtc = now; row.VoidedByUserId = actorUserId; row.VoidReason = reason;
        if (!expense.IsVoided)
        {
            expense.IsVoided = true; expense.VoidedAtUtc = now; expense.VoidedByUserId = actorUserId; expense.VoidReason = reason;
            audit.Add(propertyId, "Expense", expense.Id, "Voided", actorUserId, after: new { expense.VoidReason });
        }
        audit.Add(propertyId, PayrollAuditTypes.PayrollTransaction, row.Id, "Voided", actorUserId, after: new { reason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    private static PayrollTransactionDto ToDto(PayrollTransaction x) =>
        new(x.Id, x.EmployeeId, x.Kind, x.Amount, x.Method, x.OccurredAtUtc, x.Note, x.ExpenseId, x.IsVoided, x.VoidReason);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
