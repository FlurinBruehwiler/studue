namespace Studue;

public static class ScheduleSlots
{
    public readonly record struct Slot(TimeOnly Start, TimeOnly End);

    public static readonly List<Slot> All =
    [
        .. new[]
        {
            "08:15",
            "09:05",
            "10:15",
            "11:05",
            "13:00",
            "13:50",
            "14:50",
            "15:40",
            "16:40",
            "17:30",
            "18:40",
            "19:30",
            "20:25",
            "21:15",
        }
            .Select(TimeOnly.Parse)
            .Select(x => new Slot(x, x.AddMinutes(45))),
    ];

    public static int IndexOf(TimeOnly startTime) => All.FindIndex(x => x.Start == startTime);

    public static TimeOnly EndTimeOf(TimeOnly startTime, int duration)
    {
        var index = IndexOf(startTime);
        if (index < 0)
            return startTime.AddMinutes(45 * Math.Max(duration, 1));

        var lastSlot = Math.Min(index + Math.Max(duration, 1) - 1, All.Count - 1);
        return All[lastSlot].End;
    }
}
