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
    public void Report_media_supports_unlimited_file_count_optimized_images_and_historical_tag_snapshots()
    {
        var service = ReadRepositoryFile("src/DeLong.Web/Features/Housekeeping/HousekeepingService.cs");
        var mediaStorage = ReadRepositoryFile("src/DeLong.Web/Features/Housekeeping/RoomConditionMediaStorage.cs");
        var storage = ReadRepositoryFile("src/DeLong.Web/Features/Rooms/RoomImageStorage.cs");
        var context = ReadRepositoryFile("src/DeLong.Web/Data/AppDbContext.cs");

        Assert.Contains("files.Count < 1", service, StringComparison.Ordinal);
        Assert.DoesNotContain("files.Count is < 1 or >", service, StringComparison.Ordinal);
        Assert.Contains("HasValidVideoSignatureAsync", mediaStorage, StringComparison.Ordinal);
        Assert.Contains(".mp4", mediaStorage, StringComparison.Ordinal);
        Assert.Contains(".webm", mediaStorage, StringComparison.Ordinal);
        Assert.Contains(".mov", mediaStorage, StringComparison.Ordinal);
        Assert.Contains("TagsJson = JsonSerializer.Serialize(normalizedTags)", service, StringComparison.Ordinal);
        Assert.Contains("Rating = rating", service, StringComparison.Ordinal);
        Assert.Contains("SaveWebp(ResizeMax(source, 1600)", storage, StringComparison.Ordinal);
        Assert.Contains("NormalizeOrientation(decoded, codec.EncodedOrigin)", storage, StringComparison.Ordinal);
        Assert.Contains("entity.Property(x => x.TagsJson).HasColumnType(\"jsonb\")", context, StringComparison.Ordinal);
        Assert.Contains("ck_room_condition_reports_rating", context, StringComparison.Ordinal);
    }

    [Fact]
    public void Mobile_report_ui_supports_camera_multiple_selection_and_bottom_sheet_layout()
    {
        var page = ReadRepositoryFile("src/DeLong.Web/Pages/Admin/RoomConditionReports/Index.cshtml");
        var script = ReadRepositoryFile("src/DeLong.Web/wwwroot/js/pages/admin-room-condition-reports.js");
        var styles = ReadRepositoryFile("src/DeLong.Web/wwwroot/css/housekeeping-schedule.css");
        var program = ReadRepositoryFile("src/DeLong.Web/Program.cs");

        Assert.Contains("capture=\"environment\"", page, StringComparison.Ordinal);
        Assert.Contains("accept=\"image/*,video/*\" multiple", page, StringComparison.Ordinal);
        Assert.Contains("room-condition-table", page, StringComparison.Ordinal);
        Assert.Contains("v-for=\"value in 5\"", page, StringComparison.Ordinal);
        Assert.Contains("async function optimizeImage(file)", script, StringComparison.Ordinal);
        Assert.Contains("1920 / Math.max", script, StringComparison.Ordinal);
        Assert.Contains("this.form.content = this.form.tags.join", script, StringComparison.Ordinal);
        Assert.Contains("@media(max-width:600px)", styles, StringComparison.Ordinal);
        Assert.Contains("camera=(self), microphone=(self), geolocation=()", program, StringComparison.Ordinal);
        Assert.Contains("MultipartBodyLengthLimit", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Room_condition_reports_are_a_separate_admin_feature()
    {
        var layout = ReadRepositoryFile("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        var housekeeping = ReadRepositoryFile("src/DeLong.Web/Pages/Admin/Housekeeping/Index.cshtml");

        Assert.Contains("/Admin/RoomConditionReports/Index", layout, StringComparison.Ordinal);
        Assert.Contains("Tình trạng phòng", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("v-else-if=\"mode === 'reports'\"", housekeeping, StringComparison.Ordinal);
        Assert.DoesNotContain("v-if=\"reportForm.open\"", housekeeping, StringComparison.Ordinal);
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
