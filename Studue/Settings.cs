namespace Studue;

public class Settings
{
    public string DbFile { get; set; } = null!;
    public string FrontendUrl { get; set; } = null!;
    public string MailgunApiKey { get; set; } = null!;
    public string DatabaseBackupDir { get; set; } = null!;
}