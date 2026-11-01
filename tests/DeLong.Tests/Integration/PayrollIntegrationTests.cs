using DeLong.Web.Common.Auditing;
using DeLong.Web.Data;
using DeLong.Web.Domain.Entities;
using DeLong.Web.Domain.Enums;
using DeLong.Web.Features.Expenses;
using DeLong.Web.Features.Payroll;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DeLong.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class PayrollIntegrationTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("DELONG_TEST_CONNECTION")!).Options);

    [RoomLockPostgresFact, Trait("Category", "Integration")]
    public async Task Attendance_lock_and_linked_salary_payment_flow()
    {
        await using var db = Database();
        await db.Database.MigrateAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var property = new Property { Code = "PR" + suffix, Name = "Payroll " + suffix };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        var service = new PayrollService(db, new AuditService(db));
        var actor = (Guid?)null;
        const string month = "2026-09";

        var (employeeId, createError) = await service.SaveEmployeeAsync(property.Id, null,
            new SaveEmployeeRequest("NV01", "Nguyễn Thị Hoa", "Lễ tân", null, 4, new DateOnly(2026, 1, 1), null, true, true, 1, null, 6_000_000m, null), actor, default);
        Assert.Null(createError);
        var (_, duplicate) = await service.SaveEmployeeAsync(property.Id, null,
            new SaveEmployeeRequest("NV01", "Trùng mã", null, null, 4, new DateOnly(2026, 1, 1), null, true, true, 2, null, 5_000_000m, null), actor, default);
        Assert.Equal("duplicate", duplicate!.Code);

        // Chấm 29 ngày, để trống ngày 30: chưa chốt được.
        var cells = new List<AttendanceCellInput>();
        for (var day = 1; day <= 29; day++)
            for (var slot = 0; slot < 4; slot++)
                cells.Add(new(employeeId!.Value, new DateOnly(2026, 9, day), slot, AttendanceStatus.Present, null));
        Assert.Null(await service.SaveAttendanceAsync(property.Id, new(month, cells), actor, default));
        Assert.Equal("incomplete", (await service.LockAsync(property.Id, month, actor, default))!.Code);

        // Ô ngoài tháng hoặc ca không tồn tại bị từ chối.
        Assert.NotNull(await service.SaveAttendanceAsync(property.Id, new(month, [new(employeeId!.Value, new DateOnly(2026, 10, 1), 0, AttendanceStatus.Present, null)]), actor, default));
        Assert.NotNull(await service.SaveAttendanceAsync(property.Id, new(month, [new(employeeId.Value, new DateOnly(2026, 9, 30), 4, AttendanceStatus.Present, null)]), actor, default));

        var last = Enumerable.Range(0, 4).Select(s => new AttendanceCellInput(employeeId.Value, new DateOnly(2026, 9, 30), s, s == 3 ? AttendanceStatus.UnpaidLeave : AttendanceStatus.Present, null)).ToList();
        Assert.Null(await service.SaveAttendanceAsync(property.Id, new(month, last), actor, default));
        Assert.Null((await service.SaveAdjustmentAsync(property.Id, null, new(month, employeeId.Value, "Thưởng", 200_000m, "Khách khen"), actor, default)).Error);

        // Ứng trước khi chốt, rồi chốt.
        var advance = new CreatePayrollTransactionRequest(Guid.NewGuid(), employeeId.Value, month, PayrollTransactionKind.Advance, 1_000_000m, PaymentMethod.Cash, null, null);
        Assert.Null((await service.CreateTransactionAsync(property.Id, advance, actor, default)).Error);
        var retried = await service.CreateTransactionAsync(property.Id, advance, actor, default);
        Assert.Null(retried.Error);
        Assert.Equal(1, await db.PayrollTransactions.CountAsync(x => x.PropertyId == property.Id));
        var early = new CreatePayrollTransactionRequest(Guid.NewGuid(), employeeId.Value, month, PayrollTransactionKind.Payment, 100_000m, PaymentMethod.Cash, null, null);
        Assert.Equal("not_locked", (await service.CreateTransactionAsync(property.Id, early, actor, default)).Error!.Code);

        Assert.Null(await service.LockAsync(property.Id, month, actor, default));
        var sheet = (await service.GetSheetAsync(property.Id, month, true, default)).Sheet!;
        var line = Assert.Single(sheet.Lines!);
        Assert.Equal(6_000_000m, line.BasePay);
        Assert.Equal(50_000m, line.UnpaidDeduction);
        Assert.Equal(0m, line.AttendanceBonus);
        Assert.Equal(6_150_000m, line.Gross);
        Assert.Equal(5_150_000m, line.Remaining);

        // Sau khi chốt: không sửa chấm công, không thêm tăng/giảm, không ứng.
        Assert.Equal("locked", (await service.SaveAttendanceAsync(property.Id, new(month, last), actor, default))!.Code);
        Assert.Equal("locked", (await service.SaveAdjustmentAsync(property.Id, null, new(month, employeeId.Value, "Thưởng", 1m, "x"), actor, default)).Error!.Code);

        // Trả lương: không vượt số còn phải trả; phiếu chi liên kết không huỷ được từ màn chi phí.
        var over = new CreatePayrollTransactionRequest(Guid.NewGuid(), employeeId.Value, month, PayrollTransactionKind.Payment, 5_150_001m, PaymentMethod.BankTransfer, null, null);
        Assert.Equal("overpay", (await service.CreateTransactionAsync(property.Id, over, actor, default)).Error!.Code);
        var pay = await service.CreateTransactionAsync(property.Id, new(Guid.NewGuid(), employeeId.Value, month, PayrollTransactionKind.Payment, 3_000_000m, PaymentMethod.BankTransfer, null, "Đợt 1"), actor, default);
        Assert.Null(pay.Error);
        var expense = await db.Expenses.AsNoTracking().SingleAsync(x => x.Id == pay.Result!.ExpenseId);
        Assert.Equal(PayrollService.ExpenseSourceType, expense.SourceType);
        Assert.Equal(PayrollService.SalaryExpenseCategory, expense.Category);
        Assert.DoesNotContain("Hoa", expense.Description, StringComparison.Ordinal);
        var expenseVoid = await new ExpenseService(db, new AuditService(db)).VoidAsync(property.Id, expense.Id, "thử", null, default);
        Assert.Equal("linked_source", expenseVoid.Error!.Code);
        Assert.Equal("has_payments", (await service.UnlockAsync(property.Id, new(month, "Sửa công"), actor, default))!.Code);

        sheet = (await service.GetSheetAsync(property.Id, month, true, default)).Sheet!;
        Assert.Equal(2_150_000m, sheet.Lines![0].Remaining);

        // Huỷ khoản trả từ bảng lương thì phiếu chi huỷ theo, rồi mở chốt được.
        Assert.Null(await service.VoidTransactionAsync(property.Id, pay.Result!.Id, "Nhập nhầm", actor, default));
        db.ChangeTracker.Clear();
        Assert.True((await db.Expenses.AsNoTracking().SingleAsync(x => x.Id == expense.Id)).IsVoided);
        Assert.Null(await service.UnlockAsync(property.Id, new(month, "Sửa công"), actor, default));
        Assert.Null(await service.SaveAttendanceAsync(property.Id, new(month, last.Select(c => c with { Status = AttendanceStatus.Present }).ToList()), actor, default));

        // Nhân viên khác cơ sở không chấm được qua cơ sở này.
        var other = new Property { Code = "PO" + suffix, Name = "Other " + suffix };
        db.Properties.Add(other);
        await db.SaveChangesAsync();
        Assert.NotNull(await service.SaveAttendanceAsync(other.Id, new(month, [new(employeeId.Value, new DateOnly(2026, 9, 1), 0, AttendanceStatus.Present, null)]), actor, default));

        // Manager không nhận được trường tiền.
        var managerSheet = (await service.GetSheetAsync(property.Id, month, false, default)).Sheet!;
        Assert.Null(managerSheet.Lines);
        Assert.Null(managerSheet.Employees[0].MonthlySalary);
        Assert.True(PayrollAuditTypes.IsRestricted("PayrollTransaction"));
        Assert.False(PayrollAuditTypes.IsRestricted("Booking"));
    }
}
