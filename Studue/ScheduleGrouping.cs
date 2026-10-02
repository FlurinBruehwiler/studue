namespace Studue;

public static class ScheduleGrouping
{
    public sealed record SlotGroup(
        int Weekday,
        TimeOnly StartTime,
        int Duration,
        string ModuleCode,
        IReadOnlyList<ScheduleEntry> Entries
    );

    // The same lesson can run in several rooms at once. Shared definition of
    // "same slot", used by the schedule view, the MCP schedule tool and the iCal feed.
    // Entries must have Module loaded. Rooms within a group are not deduplicated,
    // only ordered; merging is up to the caller.
    public static IReadOnlyList<SlotGroup> GroupBySlot(IEnumerable<ScheduleEntry> entries)
    {
        return entries
            .GroupBy(x => (x.Weekday, x.StartTime, x.Duration, x.Module.Code))
            .OrderBy(x => x.Key.Weekday)
            .ThenBy(x => x.Key.StartTime)
            .Select(x => new SlotGroup(
                x.Key.Weekday,
                x.Key.StartTime,
                x.Key.Duration,
                x.Key.Code,
                x.OrderBy(y => y.Room, StringComparer.Ordinal).ToList()
            ))
            .ToList();
    }
}
