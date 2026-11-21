namespace Studue;

public static class Theme
{
    public const string CookieName = "theme";

    public const string Light = "light";
    public const string Dark = "dark";
    public const string System = "system";

    public static string FromRequest(HttpContext? context) =>
        context?.Request.Cookies[CookieName] switch
        {
            Dark => Dark,
            System => System,
            _ => Light,
        };
}
