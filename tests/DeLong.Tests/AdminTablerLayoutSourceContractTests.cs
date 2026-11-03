using System.Text.RegularExpressions;
using Xunit;

namespace DeLong.Tests;

public sealed class AdminTablerLayoutSourceContractTests
{
    [Fact]
    public void Tabler_admin_layout_links_every_admin_page_the_legacy_layout_links()
    {
        var legacy = Read("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        var next = Read("src/DeLong.Web/Pages/Shared/_AdminLayout.cshtml");

        var legacyPages = Regex.Matches(legacy, "asp-page=\"(/Admin/[^\"]+)\"").Select(x => x.Groups[1].Value).ToHashSet();
        Assert.NotEmpty(legacyPages);
        foreach (var page in legacyPages)
            Assert.True(next.Contains($"\"{page}\"", StringComparison.Ordinal), $"_AdminLayout.cshtml is missing a link to {page}");
    }

    [Fact]
    public void Tabler_admin_layout_keeps_the_hooks_used_by_shell_notification_and_ai_scripts()
    {
        var layout = Read("src/DeLong.Web/Pages/Shared/_AdminLayout.cshtml");

        foreach (var hook in new[]
                 {
                     "data-sidebar-toggle", "data-sidebar-overlay", "data-property-switcher", "data-notification-center",
                     "data-notification-toggle", "data-notification-popover", "data-notification-list", "data-ai-chat-toggle",
                     "global-toast-root", "csrf-token", "tabler.min.css", "admin-app.css"
                 })
            Assert.Contains(hook, layout, StringComparison.Ordinal);
        Assert.DoesNotContain("public.css", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("mobile-final.css", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Tabler_admin_layout_loads_heavy_scripts_only_when_a_page_asks_for_them()
    {
        var layout = Read("src/DeLong.Web/Pages/Shared/_AdminLayout.cshtml");

        foreach (var flag in new[] { "UseVue", "UseFlatpickr", "UseUploads", "UsePasswordStrength" })
            Assert.Contains($"ViewData[\"{flag}\"] is true", layout, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/DeLong.Web/Pages/Admin/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/SelectProperty.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Bookings/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Customers/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Cancellations/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Vouchers/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Housekeeping/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/RoomConditionReports/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/RoomBlocks.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/CalendarV2.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Inventory/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Cash/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Payroll/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Finance/Index.cshtml")]
    [InlineData("src/DeLong.Web/Pages/Admin/Reports/Index.cshtml")]
    public void Converted_pages_use_the_tabler_layout(string path)
    {
        Assert.Contains("Layout = \"_AdminLayout\";", Read(path), StringComparison.Ordinal);
    }

    private static string Read(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DeLongHomestay.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(dir!.FullName, relative));
    }
}
