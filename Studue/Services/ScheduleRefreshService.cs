using Microsoft.EntityFrameworkCore;

namespace Studue.Services;

// Hardly anyone presses "Reload schedule", but timetables change during the semester (ZHAW
// itself warns about the first weeks), the lesson dates from the stundenplan export only
// exist once a schedule has been fetched, and calendar subscribers may rarely open Studue.
// So the schedules of everyone active this semester are refetched once a night. Students
// still on an older semester are most likely gone; they get refetched when they sign in.
public class ScheduleRefreshService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<ScheduleRefreshService> logger
) : BackgroundService
{
    // Zurich time, when nobody is around
    private static readonly TimeOnly RefreshTime = new(3, 0);

    // stundenplan.zhaw.ch is not ours: one student at a time, with a pause in between
    private static readonly TimeSpan PauseBetweenStudents = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!environment.IsProduction())
            return;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = Helper.Now();
            var next = DateOnly.FromDateTime(now).ToDateTime(RefreshTime);
            if (next <= now)
                next = next.AddDays(1);

            await Task.Delay(next - now, stoppingToken);

            try
            {
                await RefreshAll(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Error while refreshing all schedules");
            }
        }
    }

    private async Task RefreshAll(CancellationToken stoppingToken)
    {
        var semester = Helper.GetCurrentSemester();

        List<string> studentIds;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            studentIds = await scope
                .ServiceProvider.GetRequiredService<DatabaseContext>()
                .Students.Where(x => x.LastFetchedSemester == semester)
                .Select(x => x.StudentId)
                .ToListAsync(stoppingToken);
        }

        logger.LogInformation("Refreshing the schedules of {count} students", studentIds.Count);

        var failed = new List<string>();
        foreach (var studentId in studentIds)
        {
            if (!await Refresh(studentId, stoppingToken))
                failed.Add(studentId);

            await Task.Delay(PauseBetweenStudents, stoppingToken);
        }

        logger.LogInformation(
            "Refreshed {succeeded} of {count} schedules, failed: {failed}",
            studentIds.Count - failed.Count,
            studentIds.Count,
            string.Join(", ", failed)
        );
    }

    private async Task<bool> Refresh(string studentId, CancellationToken stoppingToken)
    {
        try
        {
            // a fresh scope per student, so one failed save cannot poison the next
            await using var scope = scopeFactory.CreateAsyncScope();
            var databaseContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
            var studentContext = scope.ServiceProvider.GetRequiredService<StudentContext>();

            var student = await databaseContext.Students.FirstAsync(
                x => x.StudentId == studentId,
                stoppingToken
            );

            if (!await studentContext.FetchModulesForStudent(student))
                return false;

            await databaseContext.SaveChangesAsync(stoppingToken);
            return true;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Failed to refresh the schedule of {studentId}", studentId);
            return false;
        }
    }
}
