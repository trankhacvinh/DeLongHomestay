using DeLong.Web.Features.Site;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicShellNavTests
{
    private static PublicShellSettingsDto Settings(params PublicNavigationItemDto[] items) => new(
        "Trang chủ", "Phòng", "Cơ sở", "Đặt phòng", "Tra cứu", "Đặt ngay", "@booking",
        items.Select(x => x.Id).ToList(), items, true, true, true,
        "", "", "", "", "", "", "", true);

    [Fact]
    [Trait("Category", "Unit")]
    public void Server_menu_uses_saved_labels_order_and_marks_the_current_page()
    {
        var settings = Settings(
            new("lookup", "Tra cứu đơn", "@lookup", true, false, true),
            new("home", "Nhà", "@home", true, false, true),
            new("hidden", "Ẩn", "/an", false, false, false),
            new("menu", "Thực đơn", "https://example.com/menu", true, true, false));

        var links = PublicShellNav.Build(settings, null, "lookup", "/booking/lookup", branchesAvailable: false);

        Assert.Equal(["Tra cứu đơn", "Nhà", "Thực đơn"], links.Select(x => x.Label));
        Assert.Equal(["/booking/lookup", "/", "https://example.com/menu"], links.Select(x => x.Href));
        Assert.True(links[0].IsCurrent);
        Assert.False(links[1].IsCurrent);
        Assert.True(links[2].OpenInNewTab);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Branch_pages_get_branch_links_and_the_branch_list_item_needs_the_list()
    {
        var settings = Settings(
            new("branches", "Cơ sở", "@branches", true, false, true),
            new("booking", "Đặt phòng", "@booking", true, false, true),
            new("lookup", "Tra cứu", "@lookup", true, false, true));

        var links = PublicShellNav.Build(settings, "the-chilling", null, "/h/the-chilling", branchesAvailable: false);

        Assert.Equal(["/h/the-chilling/booking", "/h/the-chilling/booking/lookup"], links.Select(x => x.Href));
        Assert.Equal(3, PublicShellNav.Build(settings, null, "home", "/", branchesAvailable: true).Count);
        Assert.Equal("#", PublicShellNav.ResolveUrl("@unknown", null));
    }
}
