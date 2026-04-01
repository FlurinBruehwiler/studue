namespace StudueSharp;

public static class Helper
{
    public static readonly string VerifyEmailHtml;

    static Helper()
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream("StudueSharp.Email.html")!;
        using var reader = new StreamReader(stream);
        VerifyEmailHtml = reader.ReadToEnd();
    }

    public static string GetCurrentSemester()
    {
        return "FS2026";
    }
}