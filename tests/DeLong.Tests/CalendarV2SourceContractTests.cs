using Xunit;

namespace DeLong.Tests;

public sealed class CalendarV2SourceContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Standalone_calendar_v2_falls_back_to_mounted_vue_state()
    {
        var source = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-calendar-v2.js");

        Assert.Contains("root.dataset.calendarV2Page !== 'true'", source, StringComparison.Ordinal);
        Assert.Contains("root.__vue_app__?._instance?.proxy", source, StringComparison.Ordinal);
        Assert.Contains("initial.propertyId || bootVm?.propertyId", source, StringComparison.Ordinal);
        Assert.Contains("initial.today || bootVm?.today", source, StringComparison.Ordinal);
        Assert.Contains("initial.startDate || bootVm?.startDate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("const propertyId = initial.propertyId;", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Standalone_calendar_v2_never_fails_as_a_silent_blank_card()
    {
        var source = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-calendar-v2.js");

        Assert.Contains("data-calendar-v2-panel", source, StringComparison.Ordinal);
        Assert.Contains("Đang tải lịch phòng", source, StringComparison.Ordinal);
        Assert.Contains("showError", source, StringComparison.Ordinal);
        Assert.Contains("missing-property", source, StringComparison.Ordinal);
        Assert.Contains("request-error", source, StringComparison.Ordinal);
        Assert.Contains("calendar-toolbar-card[hidden]", source, StringComparison.Ordinal);
        Assert.Contains("element.style.setProperty('display', 'none', 'important')", source, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;

        if (directory is null)
            throw new InvalidOperationException("Could not locate repository root from the test output directory.");

        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
