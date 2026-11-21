namespace Studue;

// The colour theme is kept in a cookie rather than localStorage so the server can render it into
// the page: <html data-theme> is then correct in the very first byte, with no script that has
// to run before paint, and Blazor's enhanced navigation always patches in a document that
// already agrees with it. setTheme() in App.razor writes the same cookie.
public static class Theme
{
    public const string CookieName = "theme";

    public const string Light = "light";
    public const string Dark = "dark";
    public const string System = "system";

    // light, dark or system; light until a browser has chosen otherwise
    public static string FromRequest(HttpContext? context) =>
        context?.Request.Cookies[CookieName] switch
        {
            Dark => Dark,
            System => System,
            _ => Light,
        };
}
