using Xunit;

namespace DeLong.Tests;

public sealed class PublicHeaderFooterDesignerSourceContractTests
{
    [Fact]
    public void Header_capture_handler_only_claims_direct_header_editor_target()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src/DeLong.Web/wwwroot/js/pages/public-header-footer-designer.js"));

        Assert.Contains(
            "contextual?.parentElement?.matches('.public-site-header,.public-hospitality-footer')",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "contextual?.closest('.public-site-header,.public-hospitality-footer')",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc dự án.");
    }
}
