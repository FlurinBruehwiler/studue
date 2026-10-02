using System.Text.RegularExpressions;
using Ical.Net;

namespace Studue.Services;

// stundenplan.zhaw.ch can export a search result as an iCal file with one event per
// lesson that actually takes place, so holidays and moved lessons are already accounted
// for. It is only meant for a one-time import (random UIDs on every export, module codes
// instead of names, one event per room, floating times), so Studue does not hand it out.
// It only takes the real lesson dates from it and attaches them to the schedule entries
// it already knows from the weekly grid.
public static partial class ZhawCalendarExport
{
    public sealed record ExportedLesson(
        string ModuleCode,
        string TeacherId,
        string Room,
        DateOnly Date,
        TimeOnly Start,
        TimeOnly End
    );

    //"IT.DB.V (otjj)" => module code and teacher shorthand
    [GeneratedRegex(@"^(?<code>\S+)\s*(\((?<teacher>[^)]*)\))?")]
    private static partial Regex SummaryRegex { get; }

    public static List<ExportedLesson> Parse(string ics)
    {
        Calendar? calendar;
        try
        {
            calendar = Calendar.Load(ics);
        }
        catch (Exception e)
        {
            throw new FormatException("The iCal export could not be parsed.", e);
        }

        if (calendar == null)
            throw new FormatException("The iCal export contains no calendar.");

        var lessons = new List<ExportedLesson>();
        foreach (var calendarEvent in calendar.Events)
        {
            if (calendarEvent.Start == null || calendarEvent.End == null)
                continue;

            var match = SummaryRegex.Match(calendarEvent.Summary?.Trim() ?? "");
            if (!match.Success)
                continue;

            // the times carry no time zone and are Zurich wall-clock time, like the grid
            var start = calendarEvent.Start.Value;
            var end = calendarEvent.End.Value;

            lessons.Add(
                new ExportedLesson(
                    StudentContext.NormalizeModuleCode(match.Groups["code"].Value),
                    match.Groups["teacher"].Value.Trim(),
                    calendarEvent.Location?.Trim() ?? "",
                    DateOnly.FromDateTime(start),
                    TimeOnly.FromDateTime(start),
                    TimeOnly.FromDateTime(end)
                )
            );
        }

        return lessons;
    }

    // Replaces the occurrences of the given entries with the exported lessons. Each lesson
    // goes to the entry of its module that fits it best: same room first, then same slot
    // in the weekly grid, then same teacher. A lesson moved to another day still lands on
    // its entry, just as an occurrence outside the entry's slot.
    // Returns the lessons of modules none of the entries belong to.
    public static List<ExportedLesson> AssignOccurrences(
        IReadOnlyCollection<ScheduleEntry> entries,
        IEnumerable<ExportedLesson> lessons
    )
    {
        foreach (var entry in entries)
            entry.Occurrences = new List<LessonOccurrence>();

        var unmatched = new List<ExportedLesson>();
        foreach (var lesson in lessons)
        {
            var weekday = MondayBasedWeekday(lesson.Date);
            var entry = entries
                .Where(x => x.Module.Code == lesson.ModuleCode)
                .OrderByDescending(x =>
                    (x.Room == lesson.Room ? 4 : 0)
                    + (x.Weekday == weekday && x.StartTime == lesson.Start ? 2 : 0)
                    + (x.TeacherId == lesson.TeacherId ? 1 : 0)
                )
                .FirstOrDefault();

            if (entry == null)
            {
                unmatched.Add(lesson);
                continue;
            }

            if (!entry.Occurrences.Any(x => x.Date == lesson.Date && x.Start == lesson.Start))
            {
                entry.Occurrences.Add(
                    new LessonOccurrence
                    {
                        Date = lesson.Date,
                        Start = lesson.Start,
                        End = lesson.End,
                    }
                );
            }
        }

        return unmatched;
    }

    // same convention as ScheduleEntry.Weekday: 0 = Monday
    public static int MondayBasedWeekday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;
}
