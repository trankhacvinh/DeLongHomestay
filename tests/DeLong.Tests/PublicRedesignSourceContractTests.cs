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
        Assert.Contains("../fonts/brand/playfair-display-italic-500.woff", styles, StringComparison.Ordinal);
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
