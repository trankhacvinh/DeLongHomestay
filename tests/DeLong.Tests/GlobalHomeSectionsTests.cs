using System.Text.Json.Nodes;
using DeLong.Web.Pages;
using Xunit;

namespace DeLong.Tests;

public sealed class GlobalHomeSectionsTests
{
    [Fact]
    public void Main_home_removes_branch_navigation_but_keeps_configured_room_and_booking_sections()
    {
        var hero = new PublicHomeSectionVm(Guid.NewGuid(), "Hero", "Hero", "default", new JsonObject { ["secondaryUrl"] = "/#co-so", ["secondaryText"] = "Branches" });
        var rooms = new PublicHomeSectionVm(Guid.NewGuid(), "RoomGrid", "Rooms", "default", new JsonObject());
        var calendar = new PublicHomeSectionVm(Guid.NewGuid(), "AvailabilityCalendar", "Calendar", "default", new JsonObject());
        PublicHomeSectionVm[] sections = [hero, new(Guid.NewGuid(), "BranchGrid", "Branches", "default", new JsonObject()), rooms, calendar];
        var method = typeof(IndexModel).GetMethod("NormalizeGlobalSections", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var result = (IReadOnlyList<PublicHomeSectionVm>)method.Invoke(null, [sections])!;
        Assert.Equal(new[] { "Hero", "RoomGrid", "AvailabilityCalendar" }, result.Select(x => x.Type));
        Assert.Equal(string.Empty, hero.Content["secondaryUrl"]!.ToString());
        Assert.Same(rooms, result[1]);
        Assert.Same(calendar, result[2]);
    }
}
