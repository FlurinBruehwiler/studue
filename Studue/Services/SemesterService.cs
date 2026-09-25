using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Microsoft.EntityFrameworkCore;

namespace Studue.Services;

public sealed record SemesterWeek(DateOnly Start, DateOnly End);

// stundenplan.zhaw.ch only ever hands out a weekly grid, with no dates on it. The week dropdown
// on the same page is the one place the semester's real calendar dates appear, so that is where
// the semester weeks come from.
public partial class SemesterService(
    IHttpClientFactory clientFactory,
    StudueContext context,
    ILogger<SemesterService> logger
)
{
    private static string ConfigId(string semester) => $"SemesterWeeks.{semester}";

    [GeneratedRegex(@"(\d{2}\.\d{2}\.\d{4})\s*-\s*(\d{2}\.\d{2}\.\d{4})")]
    private static partial Regex WeekRangeRegex { get; }

    public async Task<List<SemesterWeek>?> GetWeeks(string semester)
    {
        var config = await context.Configs.FirstOrDefaultAsync(x => x.Id == ConfigId(semester));
        if (config != null)
            return JsonSerializer.Deserialize<List<SemesterWeek>>(config.Data);

        return await RefreshWeeks(semester);
    }

    // for hot paths that must not wait on (or fail with) stundenplan.zhaw.ch
    public async Task<List<SemesterWeek>?> GetCachedWeeks(string semester)
    {
        var config = await context.Configs.FirstOrDefaultAsync(x => x.Id == ConfigId(semester));
        return config == null ? null : JsonSerializer.Deserialize<List<SemesterWeek>>(config.Data);
    }

    public async Task<List<SemesterWeek>?> RefreshWeeks(string semester)
    {
        var weeks = await FetchWeeks(semester);
        if (weeks == null)
            return null;

        var data = JsonSerializer.Serialize(weeks);

        var config = await context.Configs.FirstOrDefaultAsync(x => x.Id == ConfigId(semester));
        if (config == null)
            context.Configs.Add(new Config { Id = ConfigId(semester), Data = data });
        else
            config.Data = data;

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // two requests can race into the same new semester; the row the other one wrote is
            // just as good as ours, and the weeks we return are unaffected either way
            logger.LogInformation(
                "Semester weeks for {semester} were stored concurrently",
                semester
            );
        }

        return weeks;
    }

    private async Task<List<SemesterWeek>?> FetchWeeks(string semester)
    {
        logger.LogInformation("Fetching semester weeks for {semester}", semester);

        using var client = clientFactory.CreateClient();

        var response = await client.PostAsync(
            "https://stundenplan.zhaw.ch/",
            new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("ctl00$SelectionContent$txtSearch", ""),
                    new KeyValuePair<string, string>("ctl00$SelectionContent$selDepartment", "T"),
                    new KeyValuePair<string, string>(
                        "ctl00$SelectionContent$selPeriodVersion",
                        semester
                    ),
                ]
            )
        );

        var parser = new HtmlParser();
        var document = parser.ParseDocument(await response.Content.ReadAsStringAsync());

        var weeks = new List<SemesterWeek>();
        foreach (var option in document.QuerySelectorAll("#SelectionContent_selWeek option"))
        {
            //"14.09.2026 - 20.09.2026 (38)"
            var match = WeekRangeRegex.Match(option.TextContent);
            if (!match.Success)
                continue;

            weeks.Add(
                new SemesterWeek(ParseDate(match.Groups[1].Value), ParseDate(match.Groups[2].Value))
            );
        }

        if (weeks.Count == 0 || !MatchesSemester(semester, weeks[0].Start))
        {
            // the page preselects an unrelated period version, so a request it does not
            // understand comes back looking perfectly fine - with somebody else's dates
            logger.LogWarning(
                "Week list for {semester} was empty or out of range, not caching it",
                semester
            );
            return null;
        }

        return weeks;
    }

    private static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static bool MatchesSemester(string semester, DateOnly firstDay)
    {
        if (semester.Length != 6 || !int.TryParse(semester.AsSpan(2), out var year))
            return false;

        if (firstDay.Year != year)
            return false;

        //a fall semester starts after the summer, a spring semester in late winter
        return semester.StartsWith("HS", StringComparison.Ordinal)
            ? firstDay.Month is >= 8 and <= 10
            : firstDay.Month is >= 1 and <= 3;
    }
}
