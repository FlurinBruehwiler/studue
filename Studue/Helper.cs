using AngleSharp.Dom;

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

    public static string GetCurrentSemester()
    {
        var currentYear = DateTime.Now.Year;

        var endOfSpringSemester = new DateTime(currentYear, 6, 29);
        var endOfFallSemester = new DateTime(currentYear, 2, 1);

        if (DateTime.Now < endOfFallSemester) return $"HS{currentYear - 1}";
        if (DateTime.Now > endOfSpringSemester) return $"HS{currentYear}";
        return $"FS{currentYear}";
    }

    public static string CreateLink(HomePageUrlInfo info, string url)
    {
        if (info.ShowOverdue)
        {
            var u = new Url(url);
            if (!string.IsNullOrEmpty(u.Query))
            {
                return u + "&overdue=true";
            }
            return new Url(new Url(url), "?overdue=true").ToString();
        }

        return url;
    }

    public class HomePageUrlInfo
    {
        public bool ShowOverdue;
    }

    private static TimeZoneInfo zurichTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public static DateTime Now()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zurichTimeZone);
    }
}