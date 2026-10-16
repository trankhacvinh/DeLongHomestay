namespace DeLong.Web.Features.Housekeeping;

public sealed record HousekeepingBusyInterval(Guid RoomId, DateTime StartUtc, DateTime EndUtc);

public static class HousekeepingSchedulePlanner
{
    private const string ConflictWarning = "Không đủ thời gian an toàn hoặc trùng lịch lao công; cần phân công thủ công.";

    public static IReadOnlyList<HousekeepingScheduleTaskDto> Plan(
        IReadOnlyList<HousekeepingScheduleTaskDto> tasks,
        IReadOnlyList<HousekeepingBusyInterval> busy,
        IReadOnlyDictionary<Guid, DateTime> arrivalDeadlines,
        DateOnly date,
        TimeZoneInfo timeZone,
        HousekeepingSettingsDto settings,
        IReadOnlyList<HousekeepingBusyInterval>? adjacentCleaning = null)
    {
        if (!settings.StaggerEnabled) return tasks;
        if (settings.CleaningMinutes <= 0 || settings.WorkerCount <= 0 || settings.MaxAdvanceMinutes < 0 ||
            settings.WorkStart is null || settings.WorkEnd is null || settings.WorkStart >= settings.WorkEnd)
            throw new ArgumentException("Cấu hình giãn lịch không hợp lệ.", nameof(settings));

        var workStart = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(settings.WorkStart.Value), timeZone);
        var workEnd = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(settings.WorkEnd.Value), timeZone);
        var duration = TimeSpan.FromMinutes(settings.CleaningMinutes);
        var result = tasks.ToArray();
        var reserved = (adjacentCleaning ?? []).ToList();
        bool IsSafe(HousekeepingScheduleTaskDto task, DateTime start)
        {
            var end = start + duration;
            if (start < workStart || end > workEnd) return false;
            if ((task.Kind == "prepare" || task.Action == "Mở đèn") &&
                arrivalDeadlines.TryGetValue(task.BookingId, out var deadline) && end > deadline) return false;
            return !busy.Any(x => x.RoomId == task.RoomId && x.StartUtc < end && start < x.EndUtc);
        }
        bool HasCapacity(Guid roomId, DateTime start)
        {
            var end = start + duration;
            var overlaps = reserved.Where(x => x.StartUtc < end && start < x.EndUtc).ToList();
            if (overlaps.Any(x => x.RoomId == roomId)) return false;
            // Count simultaneous jobs, rather than all jobs that touch this interval.
            var instants = overlaps.Select(x => x.StartUtc > start ? x.StartUtc : start).Append(start).Distinct();
            return instants.All(at => overlaps.Count(x => x.StartUtc <= at && at < x.EndUtc) < settings.WorkerCount);
        }
        // Checkout cleaning stays at its existing time. Only arrival preparation may move earlier.
        foreach (var task in tasks.Where(x => x.Kind != "prepare"))
            reserved.Add(new(task.RoomId, task.AtUtc, task.AtUtc + duration));

        var pending = tasks.Select((task, index) => (Task: task, Index: index))
            .Where(x => x.Task.Kind == "prepare")
            .Select(x => (x.Task, x.Index, Candidates: Enumerable.Range(0, settings.MaxAdvanceMinutes + 1)
                .Select(minutes => x.Task.AtUtc.AddMinutes(-minutes))
                .Where(at => IsSafe(x.Task, at)).ToList()))
            .OrderBy(x => x.Task.AtUtc)
            .ThenBy(x => x.Candidates.Count)
            .ThenBy(x => x.Index);
        foreach (var item in pending)
        {
            var selected = item.Candidates.Where(at => HasCapacity(item.Task.RoomId, at)).Cast<DateTime?>().FirstOrDefault();
            var at = selected ?? item.Task.AtUtc;
            result[item.Index] = item.Task with
            {
                AtUtc = at,
                OriginalAtUtc = at == item.Task.AtUtc ? null : item.Task.AtUtc,
                SchedulingWarning = selected is null ? ConflictWarning : null
            };
            reserved.Add(new(item.Task.RoomId, at, at + duration));
        }
        // Mark both sides of unresolved collisions, including unchanged checkout tasks.
        for (var index = 0; index < result.Length; index++)
        {
            var task = result[index];
            var end = task.AtUtc + duration;
            var others = result.Where((_, otherIndex) => otherIndex != index)
                .Select(x => new HousekeepingBusyInterval(x.RoomId, x.AtUtc, x.AtUtc + duration))
                .Concat(adjacentCleaning ?? []).Where(x => x.StartUtc < end && task.AtUtc < x.EndUtc).ToList();
            var instants = others.Select(x => x.StartUtc > task.AtUtc ? x.StartUtc : task.AtUtc).Append(task.AtUtc).Distinct();
            if (!IsSafe(task, task.AtUtc) || others.Any(x => x.RoomId == task.RoomId) ||
                instants.Any(at => others.Count(x => x.StartUtc <= at && at < x.EndUtc) >= settings.WorkerCount))
                result[index] = task with { SchedulingWarning = ConflictWarning };
        }
        return result;
    }
}
