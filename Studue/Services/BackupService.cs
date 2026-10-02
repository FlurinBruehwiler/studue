using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

        var backups = Directory.GetFiles(settings.Value.DatabaseBackupDir, "*.db");
        if (backups.Length < maxBackups)
            return;

        backups = backups.Select(x => (backup: x, creationDate: File.GetCreationTimeUtc(x))).OrderBy(x => x.creationDate)
            .Select(x => x.backup).ToArray();

        foreach (var backup in backups.Take(backups.Length - maxBackups))
        {
            File.Delete(backup);
        }
    }

    public sealed record BackupFile(string Name, string Path, DateTime CreatedUtc, long Size);

    // newest first; only what is actually in the backup folder, so a name coming from a url
    // can never point anywhere else
    public static List<BackupFile> ListBackups(Settings settings)
    {
        var backupDir = Path.GetFullPath(settings.DatabaseBackupDir);
        if (!Directory.Exists(backupDir))
            return [];

        return Directory
            .GetFiles(backupDir, "*.db")
            .Select(path => new FileInfo(path))
            .Select(file => new BackupFile(file.Name, file.FullName, file.LastWriteTimeUtc, file.Length))
            .OrderByDescending(x => x.CreatedUtc)
            .ToList();
    }

    public static BackupFile? FindBackup(Settings settings, string name) =>
        ListBackups(settings).FirstOrDefault(x => x.Name == name);

    // an uploaded database becomes just another backup, restored like any other
    public static async Task<string> SaveUpload(Settings settings, Stream upload)
    {
        var backupDir = Path.GetFullPath(settings.DatabaseBackupDir);
        Directory.CreateDirectory(backupDir);

        var path = Path.Combine(backupDir, $"upload-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");
        await using var file = new FileStream(path, FileMode.CreateNew);
        await upload.CopyToAsync(file);

        return path;
    }

    // Replaces the live database with the backup. The current database is backed up first,
    // so a wrong restore can be undone by restoring that one. Migrations run afterwards,
    // since a backup from before a migration would otherwise not match the code until
    // the next restart. Returns the name of the backup taken before the restore.
    public static async Task<string> RestoreBackup(
        Settings settings,
        BackupFile backup,
        DatabaseContext databaseContext
    )
    {
        await using var source = new SqliteConnection($"Data Source={backup.Path};Mode=ReadOnly");
        await source.OpenAsync();

        // refuse anything that is not an intact SQLite database before touching the live one
        await using (var check = source.CreateCommand())
        {
            check.CommandText = "PRAGMA quick_check";
            var result = (string?)await check.ExecuteScalarAsync();
            if (result != "ok")
                throw new InvalidOperationException($"{backup.Name} failed the integrity check: {result}");
        }

        var safetyBackup = await CreateBackup(settings, "before-restore");

        await using (var destination = new SqliteConnection(GetSqliteConnectionString(settings)))
        {
            await destination.OpenAsync();
            source.BackupDatabase(destination);
        }

        await databaseContext.Database.MigrateAsync();

        return Path.GetFileName(safetyBackup);
    }

    public static async Task<string> CreateBackup(Settings settings, string? label = null)
    {
        var suffix = label == null ? "" : $"-{label}";
        var fileName = $"studueDb-{DateTime.UtcNow:yyyyMMdd-HHmmss}{suffix}.db";
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