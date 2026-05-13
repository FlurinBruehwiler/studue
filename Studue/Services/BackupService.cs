using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Studue.Services;

public class BackupService(IOptions<Settings> settings, ILogger<BackupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            try
            {
                await CreateBackup(settings.Value);
                ClearOldBackups();
                logger.LogInformation("Created backup");
            }
            catch (Exception e)
            {
                logger.LogError(e, "Backup failed!!!");
            }
        }
    }

    private void ClearOldBackups()
    {
        const int maxBackups = 10;

        var backups = Directory.GetFiles(settings.Value.DatabaseBackupDir);
        if (backups.Length < maxBackups)
            return;

        backups = backups.Select(x => (backup: x, creationDate: File.GetCreationTimeUtc(x))).OrderBy(x => x.creationDate)
            .Select(x => x.backup).ToArray();

        foreach (var backup in backups.Take(backups.Length - maxBackups))
        {
            File.Delete(backup);
        }
    }

    public static async Task<string> CreateBackup(Settings settings)
    {
        var fileName = $"studueDb-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db";
        var backupDir = Path.GetFullPath(settings.DatabaseBackupDir);
        var backupPath = Path.Combine(Path.GetFullPath(settings.DatabaseBackupDir), fileName);

        Directory.CreateDirectory(backupDir);

        await using var source = new SqliteConnection(GetSqliteConnectionString(settings));
        await source.OpenAsync();
        var backupConnectionString = $"Data Source={backupPath}";

        await using (var destination = new SqliteConnection(backupConnectionString))
        {
            await destination.OpenAsync();
            source.BackupDatabase(destination);
            await destination.CloseAsync();
        }

        await source.CloseAsync();

        return backupPath;
    }

    public static string GetSqliteConnectionString(Settings settings)
    {
        return $"Data Source={settings.DbFile}";
    }
}