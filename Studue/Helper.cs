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
        return "FS2026";
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