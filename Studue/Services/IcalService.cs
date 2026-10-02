using System.Text;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Studue.Services;

public static class IcalService
{
    // All schedule times and assignment due dates are Zurich wall-clock time
    // (see Helper.Now()), so the feed must say so instead of emitting floating time.
    private const string TimeZoneId = "Europe/Zurich";

    public static void RegisterEndpoint(WebApplication webApplication)
    {
        // calendar servers may probe a subscription url with HEAD before fetching it;
        // Kestrel drops the body for HEAD on its own
        webApplication.MapMethods("/ical/{studentId}", ["GET", "HEAD"], Get);
    }

    private static async Task<IResult> Get(
        string studentId,
        DatabaseContext databaseContext,
        SemesterService semesterService
    )
    {
        studentId = studentId.Trim().ToLowerInvariant();

        // Only the current semester, like the rest of the app. Outlook silently drops
        // every event past roughly the 300th, and with past semesters included the
        // current one ended up at the end of the file, where it got cut off.
        var currentSemester = Helper.GetCurrentSemester();

        var student = await databaseContext.Students
            .Where(x => x.StudentId == studentId)
            .Include(x => x.ScheduleEntries.Where(se => se.Semester == currentSemester))
            .ThenInclude(x => x.Module)
            .Include(x => x.ModuleInstances.Where(mi => mi.Semester == currentSemester))
            .ThenInclude(x => x.ScheduleEntries)
            .ThenInclude(x => x.Module)
            .Include(x => x.ModuleInstances.Where(mi => mi.Semester == currentSemester))
            .ThenInclude(x => x.Assignements)
            .FirstOrDefaultAsync();

        if (student == null)
            return Results.NotFound();

        var calendar = new Calendar
        {
            Method = CalendarMethods.Publish,
            ProductId = "-//Studue//Class Schedule//DE",
        };
        calendar.AddTimeZone(
            VTimeZone.FromDateTimeZone(TimeZoneId, new DateTime(2026, 1, 1), false)
        );

        foreach (var semesterGroup in student.ModuleInstances.GroupBy(x => x.Semester))
        {
            var semester = semesterGroup.Key;

            // only needed for entries without dates from the stundenplan export
            List<SemesterWeek>? weeks = null;

            // The same lesson can run in several rooms at once; one event, every room.
            var slots = ScheduleGrouping.GroupBySlot(
                StudentContext.ScheduleEntriesOf(student, semester)
            );

            foreach (var slot in slots)
            {
                var ids = string.Join("-", slot.Entries.Select(x => x.Id).OrderBy(id => id));

                // An occurrence in the entry's own slot is a regular weekly lesson; one on
                // another day or at another time is a moved lesson.
                var occurrences = slot.Entries.SelectMany(entry =>
                    entry.Occurrences.Select(occurrence =>
                        (
                            Entry: entry,
                            Occurrence: occurrence,
                            IsRegular: occurrence.Start == entry.StartTime
                                && ZhawCalendarExport.MondayBasedWeekday(occurrence.Date)
                                    == entry.Weekday
                        )
                    )
                ).ToList();

                List<DateOnly> regularDates;
                var endTime = ScheduleSlots.EndTimeOf(slot.StartTime, slot.Duration);
                if (occurrences.Count > 0)
                {
                    var regular = occurrences.Where(x => x.IsRegular).ToList();
                    regularDates = regular
                        .Select(x => x.Occurrence.Date)
                        .Distinct()
                        .Order()
                        .ToList();

                    // the exported end is exact even when the slot grid has changed
                    if (regular.Count > 0)
                    {
                        endTime = regular
                            .GroupBy(x => x.Occurrence.End)
                            .MaxBy(x => x.Count())!
                            .Key;
                    }
                }
                else
                {
                    // No export data (yet): every semester week, without holidays.
                    // A missing week list only skips the classes, never the assignments.
                    weeks ??= await semesterService.GetWeeks(semester) ?? [];
                    regularDates = weeks
                        .Select(week => DateForWeekday(week, slot.Weekday))
                        .OfType<DateOnly>()
                        .ToList();
                }

                if (regularDates.Count > 0)
                {
                    calendar.Events.Add(
                        WeeklyClassEvent(
                            $"class-{semester}-{ids}@studue.ch",
                            slot,
                            regularDates,
                            endTime
                        )
                    );
                }

                foreach (
                    var moved in occurrences
                        .Where(x => !x.IsRegular)
                        .GroupBy(x => (x.Occurrence.Date, x.Occurrence.Start, x.Occurrence.End))
                )
                {
                    var (date, start, end) = moved.Key;
                    calendar.Events.Add(
                        ClassEvent(
                            $"class-{semester}-{ids}-{date:yyyyMMdd}T{start:HHmm}@studue.ch",
                            moved.Select(x => x.Entry).ToList(),
                            date.ToDateTime(start),
                            date.ToDateTime(end)
                        )
                    );
                }
            }

            foreach (var moduleInstance in semesterGroup)
            {
                foreach (
                    var assignment in moduleInstance.Assignements.Where(x => !x.IsDeleted)
                )
                {
                    // No time entered in the form is stored as midnight (see
                    // AssignmentService.SetValues defaulting to TimeOnly.MinValue),
                    // so 00:00 means "date only" and becomes an all-day event.
                    // A DATE DTSTART without DTEND/DURATION is one day per RFC 5545,
                    // so no DTEND is emitted in that case.
                    var isAllDay = assignment.DueDateTime.TimeOfDay == TimeSpan.Zero;
                    var dueDate = DateOnly.FromDateTime(assignment.DueDateTime);

                    calendar.Events.Add(
                        new CalendarEvent
                        {
                            Uid = $"assignment-{assignment.Id}@studue.ch",
                            Summary = $"Assignment: {assignment.Title}",
                            Description = assignment.Description,
                            Start = isAllDay
                                ? new CalDateTime(dueDate)
                                : new CalDateTime(assignment.DueDateTime, TimeZoneId),
                            End = isAllDay
                                ? null
                                : new CalDateTime(
                                    assignment.DueDateTime.AddMinutes(30),
                                    TimeZoneId
                                ),
                        }
                    );
                }
            }
        }

        var serialized =
            new CalendarSerializer().SerializeToString(calendar)
            ?? throw new InvalidOperationException("Failed to serialize the iCal calendar.");
        return Results.File(
            Encoding.UTF8.GetBytes(serialized),
            "text/calendar; charset=utf-8",
            $"{student.StudentId}.ics"
        );
    }

    // One recurring event per slot instead of one event per week: Outlook silently drops
    // every event past roughly the 300th, and a semester of single events gets close.
    // The weeks between the first and the last lesson without one (holidays) become
    // exceptions. COUNT rather than UNTIL, since UNTIL would have to be in UTC.
    private static CalendarEvent WeeklyClassEvent(
        string uid,
        ScheduleGrouping.SlotGroup slot,
        List<DateOnly> dates,
        TimeOnly endTime
    )
    {
        var first = dates[0];
        var calendarEvent = ClassEvent(
            uid,
            slot.Entries,
            first.ToDateTime(slot.StartTime),
            first.ToDateTime(endTime)
        );

        var weekCount = (dates[^1].DayNumber - first.DayNumber) / 7 + 1;
        if (weekCount > 1)
        {
            calendarEvent.RecurrenceRule = new RecurrencePattern(FrequencyType.Weekly)
            {
                Count = weekCount,
            };

            var lessonDates = dates.ToHashSet();
            for (var week = 1; week < weekCount; week++)
            {
                var date = first.AddDays(7 * week);
                if (!lessonDates.Contains(date))
                {
                    calendarEvent.ExceptionDates.Add(
                        new CalDateTime(date.ToDateTime(slot.StartTime), TimeZoneId)
                    );
                }
            }
        }

        return calendarEvent;
    }

    private static CalendarEvent ClassEvent(
        string uid,
        IReadOnlyList<ScheduleEntry> entries,
        DateTime start,
        DateTime end
    )
    {
        var rooms = string.Join(" / ", entries.Select(x => x.Room).Distinct(StringComparer.Ordinal));
        var teachers = string.Join(
            ", ",
            entries.Select(x => x.Teacher).Distinct(StringComparer.Ordinal)
        );

        return new CalendarEvent
        {
            Uid = uid,
            Summary = entries[0].Module.Name,
            Description = $"Teacher: {teachers}\nRooms: {rooms}",
            Location = rooms,
            Start = new CalDateTime(start, TimeZoneId),
            End = new CalDateTime(end, TimeZoneId),
        };
    }

    // Weekday follows the schedule grid: 0 = Monday .. 5 = Saturday
    // (see ScheduleComponent._weekdays and StudentContext week parsing).
    // Semester weeks come from stundenplan.zhaw.ch as date ranges and are not
    // guaranteed to start on a Monday, so derive the offset from the week's
    // actual start instead of assuming semesterStart.AddDays(weekday).
    private static DateOnly? DateForWeekday(SemesterWeek week, int weekday)
    {
        var weekStartMondayBased = ((int)week.Start.DayOfWeek + 6) % 7;
        var date = week.Start.AddDays(weekday - weekStartMondayBased);
        if (date < week.Start || date > week.End)
            return null;
        return date;
    }
}
