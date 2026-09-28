using DeLong.Web.Features.Staff;
using Xunit;

namespace DeLong.Tests;

public sealed class MediaStaffAuthorizationSourceContractTests
{
    [Fact]
    public void Media_role_is_available_and_has_a_property_scoped_editor_policy()
    {
        Assert.Contains(StaffRoles.Media, StaffRoles.All);

        var program = Read("src/DeLong.Web/Program.cs");
        Assert.Contains("RequireRole(\"Admin\", \"Manager\", \"Media\")", program, StringComparison.Ordinal);
        Assert.Contains("AuthorizePage(\"/Admin/Rooms/Content\", \"ManageMediaContent\")", program, StringComparison.Ordinal);
        Assert.Contains("AuthorizeFolder(\"/Admin/Pricing\", \"ViewOperations\")", program, StringComparison.Ordinal);
        Assert.DoesNotContain("options.AddPolicy(\"ManageBookings\", policy => policy.RequireRole(\"Admin\", \"Manager\", \"Staff\", \"Media\")", program, StringComparison.Ordinal);
        Assert.DoesNotContain("options.AddPolicy(\"ManageFinance\", policy => policy.RequireRole(\"Admin\", \"Manager\", \"Media\")", program, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/DeLong.Web/Features/Rooms/RoomContentEndpoints.cs")]
    [InlineData("src/DeLong.Web/Features/Site/MediaLibraryEndpoints.cs")]
    [InlineData("src/DeLong.Web/Features/Site/PropertyEditorialContentEndpoints.cs")]
    public void Media_mutations_require_role_and_property_access(string path)
    {
        var source = Read(path);
        Assert.Contains("RequireAuthorization(\"ManageMediaContent\")", source, StringComparison.Ordinal);
        Assert.Contains("AddEndpointFilter<PropertyAccessFilter>()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Media_navigation_only_exposes_room_and_editorial_tools()
    {
        var layout = Read("src/DeLong.Web/Pages/Shared/_Layout.cshtml");
        Assert.Contains("canViewRooms = canViewOperations || User.IsInRole(\"Media\")", layout, StringComparison.Ordinal);
        Assert.Contains("canManageMedia = canManageSite || User.IsInRole(\"Media\")", layout, StringComparison.Ordinal);
        Assert.Contains("@if (canManageSite)", layout, StringComparison.Ordinal);
        Assert.Contains("@if (canViewPricing)", layout, StringComparison.Ordinal);

        var staffPage = Read("src/DeLong.Web/Pages/Admin/Staff/Index.cshtml");
        Assert.Contains("Tài khoản chỉ nhìn thấy dữ liệu của các cơ sở được chọn.", staffPage, StringComparison.Ordinal);
        Assert.Contains("v-model=\"form.propertyIds\"", staffPage, StringComparison.Ordinal);
    }

    private static string Read(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DeLongHomestay.sln")))
            directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Could not locate repository root.");
        return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
    }
}
