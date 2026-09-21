namespace Studue;

public static class Helper
{
    public static readonly string VerifyEmailHtml;

    static Helper()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("Studue.Email.html")!;
        using var reader = new StreamReader(stream);
        VerifyEmailHtml = reader.ReadToEnd();
    }

    //"TS O1.23" -> "TS"; "Online asynchron" has no building, so null
    public static string? BuildingCode(string room)
    {
        var first = room.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first is { Length: 2 } && first.All(char.IsAsciiLetterUpper) ? first : null;
    }

    public static DateTime GetCurrentSemesterStart()
    {
        var (semester, year) = GetCurrentSemesterInfo();

        return semester == "HS"
            ? new DateTime(year, 6, 30)
            : new DateTime(year, 2, 2);
    }

    public static string GetCurrentSemester()
    {
        var (semester, year) = GetCurrentSemesterInfo();
        return $"{semester}{year}";
    }

    private static (string Semester, int Year) GetCurrentSemesterInfo()
    {
        var now = DateTime.Now;
        var year = now.Year;

        if (now < new DateTime(year, 2, 1))
            return ("HS", year - 1);

        if (now > new DateTime(year, 6, 29))
            return ("HS", year);

        return ("FS", year);
    }

    private static TimeZoneInfo zurichTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public static DateTime Now()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zurichTimeZone);
    }
}