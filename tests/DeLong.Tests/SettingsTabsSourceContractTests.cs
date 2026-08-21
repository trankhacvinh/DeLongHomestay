using Xunit;

namespace DeLong.Tests;

public sealed class SettingsTabsSourceContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Settings_are_grouped_into_accessible_tabs_with_rooms_selected_first()
    {
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Admin/Settings/Index.cshtml");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-settings.js");

        Assert.Contains("role=\"tablist\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"settings-tab-rooms\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"settings-panel-rooms\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"settings-panel-housekeeping\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"settings-panel-booking\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"settings-panel-notifications\"", page, StringComparison.Ordinal);
        Assert.Contains("activeTab: 'rooms'", script, StringComparison.Ordinal);
        Assert.Contains("selectTab(tab)", script, StringComparison.Ordinal);
        Assert.Contains("'booking'", script, StringComparison.Ordinal);
        Assert.Contains("public-rich-editor.js", page, StringComparison.Ordinal);

        var policyScript = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-booking-policy.js");
        Assert.Contains("data-booking-policy-tab", policyScript, StringComparison.Ordinal);
        Assert.Contains("window.DeLongRichEditor?.enhance", policyScript, StringComparison.Ordinal);
        Assert.Contains("allowImages: false", policyScript, StringComparison.Ordinal);
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
