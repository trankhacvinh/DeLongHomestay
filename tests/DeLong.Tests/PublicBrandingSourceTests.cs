using Xunit;

namespace DeLong.Tests;

public sealed class PublicBrandingSourceTests
{
    private static string Read(string path)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln"))) directory = directory.Parent;
        return File.ReadAllText(Path.Combine(directory!.FullName, "src/DeLong.Web", path));
    }

    [Theory]
    [InlineData("Pages/Shared/_Layout.cshtml")]
    [InlineData("Pages/Index.cshtml")]
    [InlineData("Pages/CustomPage.cshtml")]
    [InlineData("Pages/Rooms/Index.cshtml")]
    [InlineData("Pages/Blog/Index.cshtml")]
    [InlineData("Pages/Blog/Details.cshtml")]
    public void Public_views_do_not_embed_the_original_customer_brand(string path)
    {
        var source = Read(path);
        Assert.DoesNotContain("De Long", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">DL<", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Admin_sidebar_prefers_the_working_property_over_global_branding()
    {
        var source = Read("Pages/Shared/_Layout.cshtml");
        Assert.Contains("var adminBrandName = currentProperty?.Name ?? publicSiteName", source);
        Assert.Contains("@adminBrandInitials", source);
        Assert.Contains("<strong>@adminBrandName</strong>", source);
    }

    [Fact]
    public void Footer_logo_copyright_and_wordmark_use_the_resolved_brand()
    {
        var source = Read("Pages/Shared/_Layout.cshtml");
        Assert.Contains("@brandInitials", source);
        Assert.Contains("@DateTime.UtcNow.Year @publicSiteName", source);
        Assert.Contains("@publicSiteName.ToUpperInvariant()", source);
        Assert.Contains("GlobalSiteBrandingStore.ResolveAsync", Read("Pages/_ViewStart.cshtml"));
        Assert.DoesNotContain("De Long", Read("Features/Site/SiteContentService.cs"), StringComparison.OrdinalIgnoreCase);
    }
}
