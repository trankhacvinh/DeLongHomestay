using Xunit;

namespace DeLong.Tests;

public sealed class PayrollSourceContractTests
{
    [Fact]
    public void Payroll_money_endpoints_are_admin_only_and_mutations_use_antiforgery()
    {
        var program = Read("src/DeLong.Web/Program.cs");
        var endpoints = Read("src/DeLong.Web/Features/Payroll/PayrollEndpoints.cs");
        Assert.Contains("options.AddPolicy(\"ManagePayroll\", policy => policy.RequireRole(\"Admin\"))", program, StringComparison.Ordinal);
        Assert.Contains("options.AddPolicy(\"ManageAttendance\", policy => policy.RequireRole(\"Admin\", \"Manager\"))", program, StringComparison.Ordinal);
        Assert.Contains("AuthorizeFolder(\"/Admin/Payroll\", \"ManageAttendance\")", program, StringComparison.Ordinal);
        Assert.Contains("RequireAuthorization(ManagePayrollPolicy)", endpoints, StringComparison.Ordinal);
        Assert.Contains("AddEndpointFilter<PropertyAccessFilter>()", endpoints, StringComparison.Ordinal);
        var mutations = endpoints.Split("group.MapPut(", StringSplitOptions.None).Length - 1
            + endpoints.Split("admin.MapPost(", StringSplitOptions.None).Length - 1
            + endpoints.Split("admin.MapPut(", StringSplitOptions.None).Length - 1
            + endpoints.Split("admin.MapDelete(", StringSplitOptions.None).Length - 1;
        var antiforgery = endpoints.Split(".AddEndpointFilter<ApiAntiforgeryFilter>()", StringSplitOptions.None).Length - 1;
        Assert.Equal(mutations, antiforgery);
    }

    [Fact]
    public void Generic_audit_and_expense_void_do_not_expose_or_break_payroll_records()
    {
        var audit = Read("src/DeLong.Web/Common/Auditing/AuditEndpoints.cs");
        var expenses = Read("src/DeLong.Web/Features/Expenses/ExpenseService.cs");
        Assert.Contains("PayrollAuditTypes.IsRestricted(entityType)", audit, StringComparison.Ordinal);
        Assert.Contains("expense.SourceType is not null", expenses, StringComparison.Ordinal);
    }

    [Fact]
    public void Payroll_page_is_a_spreadsheet_with_sheet_tabs_and_keyboard_entry()
    {
        var page = Read("src/DeLong.Web/Pages/Admin/Payroll/Index.cshtml");
        var script = Read("src/DeLong.Web/wwwroot/js/pages/admin-payroll.js");
        var layout = Read("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        Assert.Contains("pr-fbar", page, StringComparison.Ordinal);
        Assert.Contains("pr-tabs", page, StringComparison.Ordinal);
        Assert.Contains("v-on:mousedown=\"cellDown\"", page, StringComparison.Ordinal);
        Assert.Contains("KEY_TO_STATUS", script, StringComparison.Ordinal);
        // Phím tắt vẫn chạy khi bộ gõ tiếng Việt bật hoặc focus còn ở ô tháng.
        Assert.Contains("CODE_TO_STATUS[ev.code]", script, StringComparison.Ordinal);
        Assert.Contains("active.blur()", script, StringComparison.Ordinal);
        // Chọn nhân viên có tìm kiếm, và lọc nhân viên trên bảng chấm công / bảng lương.
        Assert.Contains(".component('emp-picker', EmpPicker)", script, StringComparison.Ordinal);
        Assert.Contains("<emp-picker v-model=\"newTxn.employeeId\"", page, StringComparison.Ordinal);
        Assert.Contains("v-model.trim=\"empQuery\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"\">Chọn nhân viên</option>", page, StringComparison.Ordinal);
        Assert.Contains("/payroll/attendance", script, StringComparison.Ordinal);
        Assert.Contains("crypto.randomUUID()", script, StringComparison.Ordinal);
        Assert.Contains("asp-page=\"/Admin/Payroll/Index\"", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("@click", page, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
