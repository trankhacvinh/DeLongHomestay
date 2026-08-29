using Xunit;

namespace DeLong.Tests;

public sealed class FinancePeriodSourceContractTests
{
    [Fact]
    public void Finance_page_exposes_day_week_month_and_quarter_navigation()
    {
        var page = Read("src/DeLong.Web/Pages/Admin/Finance/Index.cshtml");
        var script = Read("src/DeLong.Web/wwwroot/js/pages/admin-finance.js");
        var pageModel = Read("src/DeLong.Web/Pages/Admin/Finance/Index.cshtml.cs");

        Assert.Contains("value=\"day\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"week\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"month\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"quarter\"", page, StringComparison.Ordinal);
        Assert.Contains("movePeriod(delta)", script, StringComparison.Ordinal);
        Assert.Contains("FinancePeriodResolver.Resolve", pageModel, StringComparison.Ordinal);
        Assert.Contains("selectedRange.EndExclusive", pageModel, StringComparison.Ordinal);
    }

    [Fact]
    public void Finance_page_has_excel_export_and_a_unified_compact_ledger()
    {
        var page = Read("src/DeLong.Web/Pages/Admin/Finance/Index.cshtml");
        var script = Read("src/DeLong.Web/wwwroot/js/pages/admin-finance.js");
        var pageModel = Read("src/DeLong.Web/Pages/Admin/Finance/Index.cshtml.cs");

        Assert.Contains("Xuất Excel", page, StringComparison.Ordinal);
        Assert.Contains("Sổ thu chi chi tiết", page, StringComparison.Ordinal);
        Assert.Contains("filteredLedgerEntries", page, StringComparison.Ordinal);
        Assert.Contains("ledgerEntries()", script, StringComparison.Ordinal);
        Assert.Contains("exportUrl()", script, StringComparison.Ordinal);
        Assert.Contains("OnGetExportAsync", pageModel, StringComparison.Ordinal);
        Assert.Contains("FinanceExcelExportService", pageModel, StringComparison.Ordinal);
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
