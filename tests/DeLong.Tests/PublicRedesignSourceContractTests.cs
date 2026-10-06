using Xunit;

namespace DeLong.Tests;

public sealed class PublicRedesignSourceContractTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Public_layout_loads_the_redesign_layer_last_and_self_hosts_brand_fonts()
    {
        var layout = ReadRepositoryFile("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        var styles = ReadRepositoryFile("src/DeLong.Web/wwwroot/css/public-redesign.css");

        var shell = layout.IndexOf("~/css/hospitality-shell.css", StringComparison.Ordinal);
        var redesign = layout.IndexOf("~/css/public-redesign.css", StringComparison.Ordinal);
        var custom = layout.IndexOf("/site/custom.css", StringComparison.Ordinal);
        Assert.True(shell >= 0 && redesign > shell, "Redesign must load after the hospitality shell.");
        Assert.True(custom > redesign, "A property's custom.css must still be able to override the redesign.");

        Assert.Contains("../fonts/brand/be-vietnam-pro-400.woff", styles, StringComparison.Ordinal);
        Assert.DoesNotContain("text-transform:uppercase;\n    letter-spacing:-", styles, StringComparison.Ordinal);
        Assert.Contains("--dl-h1:", styles, StringComparison.Ordinal);
        Assert.Contains("line-height:1.18!important", styles, StringComparison.Ordinal);
        Assert.True(File.Exists(RepositoryPath("src/DeLong.Web/wwwroot/fonts/brand/be-vietnam-pro-900.woff")));
        Assert.True(File.Exists(RepositoryPath("src/DeLong.Web/wwwroot/fonts/brand/OFL-BeVietnamPro.txt")));
        Assert.DoesNotContain("fonts.googleapis.com", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", styles, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Footer_renders_contact_cards_per_property_from_site_settings()
    {
        var layout = ReadRepositoryFile("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        var service = ReadRepositoryFile("src/DeLong.Web/Features/Site/SiteContentService.cs");

        Assert.Contains("SiteContentService.GetPublicContactsAsync", layout, StringComparison.Ordinal);
        Assert.Contains("public-footer-contact-card", layout, StringComparison.Ordinal);
        Assert.Contains("contact.ZaloUrl", layout, StringComparison.Ordinal);
        Assert.Contains("contact.FacebookUrl", layout, StringComparison.Ordinal);
        Assert.Contains("contact.GoogleMapsUrl", layout, StringComparison.Ordinal);
        Assert.Contains("href=\"tel:@contactTel\"", layout, StringComparison.Ordinal);
        Assert.Contains("public-footer-policies", layout, StringComparison.Ordinal);
        Assert.Contains("PublicCacheKeys.PropertyContacts", service, StringComparison.Ordinal);
        Assert.Contains("tags: [PublicCacheKeys.Tag]", service, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calendar_shows_checked_branches_side_by_side_and_keeps_one_room_per_selection()
    {
        var source = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/public-availability-calendar.js");
        var styles = ReadRepositoryFile("src/DeLong.Web/wwwroot/css/public-availability-calendar.css");
        var home = ReadRepositoryFile("src/DeLong.Web/Pages/Index.cshtml");

        Assert.Contains("data-calendar-branches", source, StringComparison.Ordinal);
        Assert.Contains("window.matchMedia('(min-width: 761px)')", source, StringComparison.Ordinal);
        Assert.Contains("function rejectOtherRoom(day)", source, StringComparison.Ordinal);
        Assert.Contains("if (rejectOtherRoom(day)) return;", source, StringComparison.Ordinal);
        Assert.Contains("state.selectedRoomId", source, StringComparison.Ordinal);
        Assert.Contains("daysOf(currentRoom())", source, StringComparison.Ordinal);
        Assert.Contains("index += 4", source, StringComparison.Ordinal);
        Assert.Contains(".public-v2-viewport.is-multi{overflow-x:auto}", styles, StringComparison.Ordinal);
        Assert.Contains(".public-v2-slot-cell.is-room-start", styles, StringComparison.Ordinal);
        Assert.Contains("fromPrice = item.Room.QuickFromPrice", home, StringComparison.Ordinal);
        Assert.Contains("public-hero-price-sticker", home, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Standard_theme_keeps_the_built_in_design_authoritative()
    {
        var store = ReadRepositoryFile("src/DeLong.Web/Features/Site/PublicThemeStore.cs");
        var layout = ReadRepositoryFile("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        var home = ReadRepositoryFile("src/DeLong.Web/Pages/Index.cshtml");
        var custom = ReadRepositoryFile("src/DeLong.Web/Pages/CustomPage.cshtml");
        var api = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/core/api.js");
        var endpoints = ReadRepositoryFile("src/DeLong.Web/Features/Site/SiteContentEndpoints.cs");
        var styles = ReadRepositoryFile("src/DeLong.Web/wwwroot/css/public-redesign.css");

        Assert.Contains("MetadataSectionType = \"__PublicTheme\"", store, StringComparison.Ordinal);
        Assert.Contains("return new PublicThemeDto(StandardMode);", store, StringComparison.Ordinal);
        Assert.Contains("!isGlobalPublic && !publicThemeStandard", layout, StringComparison.Ordinal);
        Assert.Contains("dl-theme-standard", layout, StringComparison.Ordinal);
        Assert.Contains("Model.ThemeStandard ? string.Empty : CustomPageModel.VisualClass", home, StringComparison.Ordinal);
        Assert.Contains("Model.ThemeStandard ? string.Empty : CustomPageModel.VisualClass", custom, StringComparison.Ordinal);
        Assert.DoesNotContain("variant-@(block.Variant)", home, StringComparison.Ordinal);
        Assert.DoesNotContain("variant-@(section.Variant)", custom, StringComparison.Ordinal);
        Assert.Contains("if (!standardTheme) addScript('data-public-shell-designer-runtime'", api, StringComparison.Ordinal);
        Assert.Contains("if (!standardTheme) addScript('data-public-visual-typography-runtime'", api, StringComparison.Ordinal);
        Assert.Contains("global.MapPut(\"/theme\"", endpoints, StringComparison.Ordinal);
        Assert.Contains("x.Type != PublicThemeStore.MetadataSectionType", endpoints, StringComparison.Ordinal);
        Assert.Contains("body.dl-theme-standard .dl-builder-row .dl-row-heading", styles, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Admin_can_restore_the_global_home_to_the_approved_layout_without_losing_old_blocks()
    {
        var service = ReadRepositoryFile("src/DeLong.Web/Features/Site/SiteContentService.cs");
        var endpoints = ReadRepositoryFile("src/DeLong.Web/Features/Site/SiteContentEndpoints.cs");
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Admin/Site/Global.cshtml");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-global-site.js");
        var home = ReadRepositoryFile("src/DeLong.Web/Pages/Index.cshtml");

        Assert.Contains("public async Task<int> ResetGlobalHomeToStandardAsync", service, StringComparison.Ordinal);
        Assert.Contains("section.IsVisible = false;", service, StringComparison.Ordinal);
        Assert.Contains("ArchivedSectionPrefix", service, StringComparison.Ordinal);
        Assert.Contains("\"AvailabilityCalendar\", \"Lịch phòng\"", service, StringComparison.Ordinal);
        Assert.Contains("global.MapPost(\"/reset-standard\"", endpoints, StringComparison.Ordinal);
        Assert.Contains("Khôi phục giao diện mặc định", page, StringComparison.Ordinal);
        Assert.Contains("resetConfirm", page, StringComparison.Ordinal);
        Assert.Contains("/api/admin/site/global/reset-standard", script, StringComparison.Ordinal);
        Assert.DoesNotContain("window.confirm('Thay", script, StringComparison.Ordinal);
        Assert.Contains("HowBand(block.Content", home, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath) => File.ReadAllText(RepositoryPath(relativePath));

    private static string RepositoryPath(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return Path.Combine(directory.FullName, relativePath);
    }
}
