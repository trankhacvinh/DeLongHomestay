using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace DeLong.Tests;

public sealed class PublicPerformanceAssetsTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "src/DeLong.Web/DeLong.Web.csproj"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [Fact]
    public void Built_home_bundle_keeps_cascade_order_and_expands_imports()
    {
        var root = Path.Combine(Root(), "src/DeLong.Web");
        var manifest = XDocument.Load(Path.Combine(root, "Build/PublicHomeCss.targets"));
        var bundle = File.ReadAllText(Path.Combine(root, "wwwroot/css/public-home.bundle.css"));
        var previous = -1;
        foreach (var input in manifest.Descendants("PublicHomeCss"))
        {
            var filename = Path.GetFileName(input.Attribute("Include")!.Value);
            var index = bundle.IndexOf($"/* {filename} */", StringComparison.Ordinal);
            Assert.True(index > previous, $"Cascade order changed or source missing: {filename}");
            previous = index;
        }
        Assert.DoesNotContain("@import", bundle);
        Assert.Contains("/* public-mobile-final.css */", bundle);
    }

    [Fact]
    public void Font_preloads_and_css_use_matching_content_fingerprints()
    {
        var root = Path.Combine(Root(), "src/DeLong.Web");
        var css = File.ReadAllText(Path.Combine(root, "wwwroot/css/public-redesign.css"));
        var layout = File.ReadAllText(Path.Combine(root, "Pages/Shared/_Layout.cshtml"));
        var references = Regex.Matches(css, @"(?<name>be-vietnam-pro-\d+\.woff2)\?v=(?<hash>[a-f0-9]+)");
        Assert.Equal(4, references.Count);
        foreach (Match reference in references)
        {
            var name = reference.Groups["name"].Value;
            var bytes = File.ReadAllBytes(Path.Combine(root, "wwwroot/fonts/brand", name));
            Assert.Equal("wOF2", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..16];
            Assert.Equal(hash, reference.Groups["hash"].Value);
            Assert.Contains($"{name}?v={hash}", layout);
        }
    }
}
