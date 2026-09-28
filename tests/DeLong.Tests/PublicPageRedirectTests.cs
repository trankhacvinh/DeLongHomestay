using DeLong.Web.Pages.Rooms;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicPageRedirectTests
{
    [Fact]
    public async Task Property_home_redirects_permanently_to_the_single_public_homepage()
    {
        var result = await new DeLong.Web.Pages.IndexModel(null!, null!, null!, null!, null!)
            .OnGetAsync("de-long", CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.True(redirect.Permanent);
        Assert.Equal("/", redirect.Url);
    }

    [Fact]
    public async Task Room_catalog_redirects_permanently_to_the_home_booking_calendar()
    {
        var result = await new IndexModel()
            .OnGetAsync("de-long", null, null, CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.True(redirect.Permanent);
        Assert.Equal("/#lich-phong", redirect.Url);
    }

    [Fact]
    public async Task Room_detail_redirects_permanently_to_the_home_booking_calendar()
    {
        var result = await new DetailsModel()
            .OnGetAsync("coco-blue", "de-long", CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.True(redirect.Permanent);
        Assert.Equal("/#lich-phong", redirect.Url);
    }
}
