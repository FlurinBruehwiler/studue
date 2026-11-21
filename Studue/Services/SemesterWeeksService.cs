namespace Studue.Services;

public class SemesterWeeksService(
    IServiceScopeFactory scopeFactory,
    ILogger<SemesterWeeksService> logger
) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var cached = false;

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var semesterService = scope.ServiceProvider.GetRequiredService<SemesterService>();
                var semester = Helper.GetCurrentSemester();

                cached =
                    await semesterService.GetWeeks(semester) != null
                    || await semesterService.RefreshWeeks(semester) != null;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Error while loading the semester weeks");
            }

            await Task.Delay(cached ? CheckInterval : RetryInterval, stoppingToken);
        }
    }
}
