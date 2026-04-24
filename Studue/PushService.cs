using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Studue.Services;
using WebPush;

namespace Studue;

public class PushSubscriptionDto
{
    public required string Endpoint { get; set; }
    public required string P256DH { get; set; }
    public required string Auth { get; set; }
}

public class PushService(IDbContextFactory<StudueContext> contextFactory, IOptions<Settings> settings, ILogger<PushService> logger) : BackgroundService
{
    public static void RegisterEndpoint(WebApplication webApplication)
    {
        webApplication.MapPost("/push/subscribe", async ([FromBody] PushSubscriptionDto subscriptionDto, StudueContext studueContext, StudentContext studentContext) =>
        {
            await studueContext.PushSubscriptions.AddAsync(new PushSubscriptionRow
            {
                Auth = subscriptionDto.Auth,
                Endpoint = subscriptionDto.Endpoint,
                P256DH = subscriptionDto.P256DH,
                Student = studentContext.Student
            });
            await studueContext.SaveChangesAsync();

            return Results.Ok();
        }).WithMetadata(new StudentRequiredAttribute());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var studueContext = await contextFactory.CreateDbContextAsync(stoppingToken);

            try
            {
                await SendNotifications(studueContext);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while sending notifications");
            }
            finally
            {
                await studueContext.SaveChangesAsync(stoppingToken);
            }
        }
    }

    private async Task SendNotifications(StudueContext studueContext)
    {
        var lastNotificationTimeConfig = await studueContext.Configs.FirstAsync(x => x.Id == "LastNotificationTime");
        var lastNotificationTime = DateTime.Parse(lastNotificationTimeConfig.Data);

        var now = Helper.Now();
        lastNotificationTimeConfig.Data = now.ToString(CultureInfo.InvariantCulture);

        var futureAssignments = await studueContext.Assignements.Where(x => x.DueDateTime > now).ToListAsync();

        (TimeSpan timespan, string dueInString)[] relevantTimes = [(TimeSpan.FromHours(1), "1 hour"), (TimeSpan.FromDays(1), "1 day")];

        foreach (var relevantTime in relevantTimes)
        {
            var assignmentsToNotify = futureAssignments.Where(x =>
            {
                var timeWhenNotificationShouldBeSent = x.DueDateTime - relevantTime.timespan;
                return timeWhenNotificationShouldBeSent > lastNotificationTime && timeWhenNotificationShouldBeSent < now;
            }).ToList();

            foreach (var assignment in assignmentsToNotify)
            {
                await SendAsync(studueContext, assignment, relevantTime.dueInString);
            }
        }
    }

    private async Task SendAsync(StudueContext studueContext, Assignment assignment, string dueIn)
    {
        var subscriptions = await studueContext.Students
            .Where(x => x.ModuleInstances.Any(y => y.Assignements.Contains(assignment)))
            .SelectMany(x => x.PushSubscriptions)
            .ToListAsync();

        logger.LogInformation("Sending {0} notifications for '{1}' assignment", subscriptions.Count, assignment.Title);

        var publicKey = await studueContext.Configs.FirstAsync(x => x.Id == "VapidKey.Public");
        var privateKey = await studueContext.Configs.FirstAsync(x => x.Id == "VapidKey.Private");

        var vapidDetails = new VapidDetails(settings.Value.FrontendUrl, publicKey.Data, privateKey.Data);

        var client = new WebPushClient();

        var payload = JsonSerializer.Serialize(new
        {
            Title = $"{assignment.Title} is due in {dueIn}",
            Url = new Uri(new Uri(settings.Value.FrontendUrl), assignment.Id.ToString()).ToString()
        });

        foreach (var batch in subscriptions.Chunk(10))
        {
            var tasks = batch.Select(async sub =>
            {
                var subscription = new PushSubscription(
                    sub.Endpoint,
                    sub.P256DH,
                    sub.Auth
                );

                try
                {
                    await client.SendNotificationAsync(subscription, payload, vapidDetails);
                }
                catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
                {
                    studueContext.PushSubscriptions.Remove(sub);
                }
            });
            await Task.WhenAll(tasks);
        }
    }
}