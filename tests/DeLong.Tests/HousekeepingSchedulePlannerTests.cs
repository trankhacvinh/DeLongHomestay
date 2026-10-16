using DeLong.Web.Features.Housekeeping;
using Xunit;

namespace DeLong.Tests;

public sealed class HousekeepingSchedulePlannerTests
{
    private static readonly DateOnly Day = new(2026, 10, 12);
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
    private static readonly HousekeepingSettingsDto Settings = new(30, 0, true, true, true, 30, 1, 30, new(8, 0), new(22, 0));
    private static DateTime At(int hour, int minute = 0) => TimeZoneInfo.ConvertTimeToUtc(Day.ToDateTime(new(hour, minute)), Zone);
    private static HousekeepingScheduleTaskDto Task(Guid room, string kind = "prepare", int hour = 10, int minute = 30) =>
        new(Guid.NewGuid(), "TEST", room, "R", "Phòng", At(hour, minute), kind, kind == "prepare" ? "Mở đèn" : "Tắt đèn", "Dọn phòng");
    private static IReadOnlyDictionary<Guid, DateTime> Deadlines(params HousekeepingScheduleTaskDto[] tasks) =>
        tasks.ToDictionary(x => x.BookingId, _ => At(11));

    [Fact]
    public void Flexible_room_moves_to_ten_while_room_occupied_until_ten_thirty_stays()
    {
        var abaus = Task(Guid.NewGuid());
        var moon = Task(Guid.NewGuid());
        var night = Task(abaus.RoomId, "turnover", 1, 0);
        var busy = new[] { new HousekeepingBusyInterval(abaus.RoomId, At(1), At(10)), new(moon.RoomId, At(8), At(10, 30)) };
        var result = HousekeepingSchedulePlanner.Plan([night, abaus, moon], busy, Deadlines(abaus, moon), Day, Zone, Settings);
        Assert.Equal(At(10), result[1].AtUtc);
        Assert.Equal(At(10, 30), result[1].OriginalAtUtc);
        Assert.Equal(At(10, 30), result[2].AtUtc);
        Assert.Null(result[1].SchedulingWarning);
        Assert.Null(result[2].SchedulingWarning);
        Assert.Equal(At(1), result[0].AtUtc);
        Assert.NotNull(result[0].SchedulingWarning);
        Assert.Equal(At(10, 30), abaus.AtUtc);
    }

    [Fact]
    public void Block_or_guest_prevents_unsafe_advance_and_marks_both_colliding_tasks()
    {
        var a = Task(Guid.NewGuid()); var b = Task(Guid.NewGuid());
        var busy = new[] { new HousekeepingBusyInterval(a.RoomId, At(1), At(10, 30)), new(b.RoomId, At(1), At(10, 30)) };
        var result = HousekeepingSchedulePlanner.Plan([a, b], busy, Deadlines(a, b), Day, Zone, Settings);
        Assert.All(result, x => { Assert.Equal(At(10, 30), x.AtUtc); Assert.NotNull(x.SchedulingWarning); });
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Two_workers_allow_same_time_and_touching_intervals_do_not_overlap()
    {
        var a = Task(Guid.NewGuid()); var b = Task(Guid.NewGuid());
        var result = HousekeepingSchedulePlanner.Plan([a, b], [], Deadlines(a, b), Day, Zone, Settings with { WorkerCount = 2 });
        Assert.All(result, x => { Assert.Equal(At(10, 30), x.AtUtc); Assert.Null(x.SchedulingWarning); });
        var c = Task(Guid.NewGuid(), "turnover", 10, 0);
        result = HousekeepingSchedulePlanner.Plan([c, a], [], Deadlines(a), Day, Zone, Settings);
        Assert.Null(result[1].SchedulingWarning);
        Assert.Equal(At(10, 30), result[1].AtUtc);
    }

    [Fact]
    public void Working_hours_deadline_and_advance_limit_are_respected()
    {
        var a = Task(Guid.NewGuid()); var b = Task(Guid.NewGuid());
        var result = HousekeepingSchedulePlanner.Plan([a, b], [], Deadlines(a, b), Day, Zone,
            Settings with { WorkStart = new(10, 15), MaxAdvanceMinutes = 15 });
        Assert.All(result, x => Assert.Equal(At(10, 30), x.AtUtc));
        Assert.All(result, x => Assert.NotNull(x.SchedulingWarning));
        result = HousekeepingSchedulePlanner.Plan([a], [], new Dictionary<Guid, DateTime> { [a.BookingId] = At(10, 15) }, Day, Zone, Settings);
        Assert.Equal(a.AtUtc, result[0].AtUtc);
        Assert.NotNull(result[0].SchedulingWarning);
    }

    [Fact]
    public void Midnight_adjacent_cleaning_consumes_capacity_without_creating_extra_tasks()
    {
        var a = Task(Guid.NewGuid(), "prepare", 0, 0);
        var adjacent = new[] { new HousekeepingBusyInterval(Guid.NewGuid(), At(0).AddMinutes(-15), At(0, 15)) };
        var result = HousekeepingSchedulePlanner.Plan([a], [], new Dictionary<Guid, DateTime> { [a.BookingId] = At(1) }, Day, Zone,
            Settings with { WorkStart = new(0, 0) }, adjacent);
        Assert.Single(result);
        Assert.Equal(a.AtUtc, result[0].AtUtc);
        Assert.NotNull(result[0].SchedulingWarning);
    }

    [Fact]
    public void Disabled_setting_preserves_existing_schedule_and_enabled_requires_work_hours()
    {
        HousekeepingScheduleTaskDto[] tasks = [Task(Guid.NewGuid())];
        Assert.Same(tasks, HousekeepingSchedulePlanner.Plan(tasks, [], Deadlines(tasks), Day, Zone, Settings with { StaggerEnabled = false }));
        Assert.Throws<ArgumentException>(() => HousekeepingSchedulePlanner.Plan(tasks, [], Deadlines(tasks), Day, Zone, Settings with { WorkStart = null }));
    }
}
