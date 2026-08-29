using Xunit;

namespace DeLong.Tests;

public sealed class ReportsDashboardSourceContractTests
{
    [Fact]
    public void Business_dashboard_exposes_daily_weekly_monthly_and_twelve_month_series()
    {
        var service = Read("src/DeLong.Web/Features/Reports/ReportService.cs");

        Assert.Contains("ReportPeriodReceiptsDto", service, StringComparison.Ordinal);
        Assert.Contains("ReportDailyDto", service, StringComparison.Ordinal);
        Assert.Contains("ReportWeeklyDto", service, StringComparison.Ordinal);
        Assert.Contains("Enumerable.Range(0, 12)", service, StringComparison.Ordinal);
        Assert.Contains("x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc < toUtc", service, StringComparison.Ordinal);
        Assert.Contains("x.CheckInUtc >= fromUtc && x.CheckInUtc < toUtc", service, StringComparison.Ordinal);
        Assert.Contains("x.Status is not BookingStatus.Cancelled and not BookingStatus.NoShow", service, StringComparison.Ordinal);
        Assert.Contains("AsNoTracking()", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_contains_line_donut_weekly_and_responsive_business_views()
    {
        var page = Read("src/DeLong.Web/Pages/Admin/Reports/Index.cshtml");
        var script = Read("src/DeLong.Web/wwwroot/js/pages/admin-reports.js");
        var styles = Read("src/DeLong.Web/wwwroot/css/finance-reports.css");

        Assert.Contains("report-line-chart", page, StringComparison.Ordinal);
        Assert.Contains("report-donut", page, StringComparison.Ordinal);
        Assert.Contains("Doanh thu theo tuần", page, StringComparison.Ordinal);
        Assert.Contains("Xu hướng dòng tiền 12 tháng", page, StringComparison.Ordinal);
        Assert.Contains("donutStyle(items)", script, StringComparison.Ordinal);
        Assert.Contains("dailyLinePoints(key)", script, StringComparison.Ordinal);
        Assert.Contains("conic-gradient", script, StringComparison.Ordinal);
        Assert.Contains("@media (max-width:540px)", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_offer_management_breakdowns_excel_export_and_finance_precedes_website()
    {
        var page = Read("src/DeLong.Web/Pages/Admin/Reports/Index.cshtml");
        var pageModel = Read("src/DeLong.Web/Pages/Admin/Reports/Index.cshtml.cs");
        var service = Read("src/DeLong.Web/Features/Reports/ReportService.cs");
        var layout = Read("src/DeLong.Web/Pages/Shared/_Layout.cshtml");

        Assert.Contains("Chi phí theo nhóm", page, StringComparison.Ordinal);
        Assert.Contains("Hiệu quả theo thứ", page, StringComparison.Ordinal);
        Assert.Contains("Tuổi công nợ", page, StringComparison.Ordinal);
        Assert.Contains("Xuất Excel", page, StringComparison.Ordinal);
        Assert.Contains("OnGetExportAsync", pageModel, StringComparison.Ordinal);
        Assert.Contains("ReportExpenseCategoryDto", service, StringComparison.Ordinal);
        Assert.Contains("ReportOutstandingBucketDto", service, StringComparison.Ordinal);
        Assert.True(
            layout.IndexOf("<div class=\"nav-label\">Tài chính</div>", StringComparison.Ordinal) <
            layout.IndexOf("<div class=\"nav-label\">Website</div>", StringComparison.Ordinal));
    }

    [Fact]
    public void Outstanding_balance_is_filtered_after_the_translatable_database_projection()
    {
        var service = Read("src/DeLong.Web/Features/Reports/ReportService.cs");
        var projection = service.IndexOf("var outstandingProjection = await db.Bookings", StringComparison.Ordinal);
        var materialization = service.IndexOf(".ToListAsync(cancellationToken);", projection, StringComparison.Ordinal);
        var balanceFilter = service.IndexOf(".Where(x => x.Balance > 0)", materialization, StringComparison.Ordinal);

        Assert.True(projection >= 0);
        Assert.True(materialization > projection);
        Assert.True(balanceFilter > materialization);
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
