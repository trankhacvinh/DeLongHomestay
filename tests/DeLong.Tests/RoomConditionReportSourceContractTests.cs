using Xunit;

namespace DeLong.Tests;

public sealed class RoomConditionReportSourceContractTests
{
    [Fact]
    public void Report_upload_is_property_scoped_authorized_and_antiforgery_protected()
    {
        var endpoints = ReadRepositoryFile("src/DeLong.Web/Features/Housekeeping/HousekeepingEndpoints.cs");

        Assert.Contains("/api/admin/properties/{propertyId:guid}/housekeeping", endpoints, StringComparison.Ordinal);
        Assert.Contains("AddEndpointFilter<PropertyAccessFilter>()", endpoints, StringComparison.Ordinal);
        Assert.Contains("group.MapPost(\"/reports\"", endpoints, StringComparison.Ordinal);
        Assert.Contains("RequireAuthorization(\"ManageHousekeeping\")", endpoints, StringComparison.Ordinal);
        Assert.Contains("AddEndpointFilter<ApiAntiforgeryFilter>()", endpoints, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_images_are_limited_optimized_and_keep_historical_tag_snapshots()
    {
        var service = ReadRepositoryFile("src/DeLong.Web/Features/Housekeeping/HousekeepingService.cs");
        var storage = ReadRepositoryFile("src/DeLong.Web/Features/Rooms/RoomImageStorage.cs");
        var context = ReadRepositoryFile("src/DeLong.Web/Data/AppDbContext.cs");

        Assert.Contains("files.Count is < 1 or > 12", service, StringComparison.Ordinal);
        Assert.Contains("TagsJson = JsonSerializer.Serialize(normalizedTags)", service, StringComparison.Ordinal);
        Assert.Contains("SaveWebp(ResizeMax(source, 1600)", storage, StringComparison.Ordinal);
        Assert.Contains("NormalizeOrientation(decoded, codec.EncodedOrigin)", storage, StringComparison.Ordinal);
        Assert.Contains("entity.Property(x => x.TagsJson).HasColumnType(\"jsonb\")", context, StringComparison.Ordinal);
    }

    [Fact]
    public void Mobile_report_ui_supports_camera_multiple_selection_and_bottom_sheet_layout()
    {
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Admin/Housekeeping/Index.cshtml");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-housekeeping.js");
        var styles = ReadRepositoryFile("src/DeLong.Web/wwwroot/css/housekeeping-schedule.css");
        var program = ReadRepositoryFile("src/DeLong.Web/Program.cs");

        Assert.Contains("capture=\"environment\"", page, StringComparison.Ordinal);
        Assert.Contains("accept=\"image/*\" multiple", page, StringComparison.Ordinal);
        Assert.Contains("async function optimizePhoto(file)", script, StringComparison.Ordinal);
        Assert.Contains("maxEdge = 1920", script, StringComparison.Ordinal);
        Assert.Contains("@media(max-width:600px)", styles, StringComparison.Ordinal);
        Assert.Contains("camera=(self), microphone=(), geolocation=()", program, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
