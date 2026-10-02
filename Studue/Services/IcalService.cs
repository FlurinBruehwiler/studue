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

        var student = await databaseContext.Students
            .Where(x => x.StudentId == studentId)
            .Include(x => x.ModuleInstances)
            .ThenInclude(x => x.ScheduleEntries)
            .ThenInclude(x => x.Module)
            .Include(x => x.ModuleInstances)
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

            // Classes need semester weeks to be placed on real dates, assignments don't.
            // A missing week list must only skip the classes, never the assignments.
            var weeks = await semesterService.GetWeeks(semester);
            if (weeks is { Count: > 0 })
            {
                // The same lesson can run in several rooms at once; one event,
                // every room.
                var slots = ScheduleGrouping.GroupBySlot(
                    semesterGroup.SelectMany(mi => mi.ScheduleEntries)
                );

                foreach (var slot in slots)
                {
                    var endTime = ScheduleSlots.EndTimeOf(
                        slot.StartTime,
                        slot.Duration
                    );
                    var ids = string.Join(
                        "-",
                        slot.Entries.Select(x => x.Id).OrderBy(id => id)
                    );

                    // One VEVENT per actual teaching week: the week list has gaps for
                    // holidays, which a weekly RRULE from semester start to end would
                    // incorrectly fill with classes.
                    foreach (var week in weeks)
                    {
                        var date = DateForWeekday(week, slot.Weekday);
                        if (date is null)
                            continue;

                        var start = date.Value.ToDateTime(slot.StartTime);
                        var end = date.Value.ToDateTime(endTime);

                        calendar.Events.Add(
                            new CalendarEvent
                            {
                                Uid =
                                    $"class-{semester}-{ids}-{date.Value:yyyyMMdd}@studue.ch",
                                Summary = slot.Entries.First().Module.Name,
                                Description = string.Join(
                                    "\n",
                                    [
                                        $"Teacher: {string.Join(", ", slot.Entries.Select(x => x.Teacher).Distinct(StringComparer.Ordinal))}",
                                        $"Rooms: {string.Join(" / ", slot.Entries.Select(x => x.Room).Distinct(StringComparer.Ordinal))}",
                                    ]
                                ),
                                Location = string.Join(
                                    " / ",
                                    slot.Entries
                                        .Select(x => x.Room)
                                        .Distinct(StringComparer.Ordinal)
                                ),
                                Start = new CalDateTime(start, TimeZoneId),
                                End = new CalDateTime(end, TimeZoneId),
                            }
                        );
                    }
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
