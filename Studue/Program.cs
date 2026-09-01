using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Studue;
using Studue.Components;
using Studue.Services;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, config) => { config.ReadFrom.Configuration(context.Configuration); });

    builder.Services.Configure<Settings>(builder.Configuration.GetSection("Studue"));
    builder.Services.PostConfigure<Settings>(settings =>
    {
        settings.DbFile = Path.GetFullPath(settings.DbFile, builder.Environment.ContentRootPath);
        settings.DatabaseBackupDir = Path.GetFullPath(settings.DatabaseBackupDir, builder.Environment.ContentRootPath);
    });
    builder.Services.AddScoped<StudentContext>();
    builder.Services.AddSingleton<BuildingIndex>();
    builder.Services.AddDbContextFactory<StudueContext>((services, options) =>
    {
        options.UseSqlite(BackupService.GetSqliteConnectionString(services.GetRequiredService<IOptions<Settings>>().Value),
            o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
    });

    builder.Services.AddHostedService<BackupService>();
    builder.Services.AddHttpClient();

    builder.Services.AddAuthentication(AdminAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, AdminAuthenticationHandler>(AdminAuthenticationHandler.SchemeName, _ => { });
    builder.Services.AddAuthorization();

    builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    builder.Services.AddHostedService<PushService>();

    var app = builder.Build();

    Log.Information("Using SQLite database at {DbFile}", app.Services.GetRequiredService<IOptions<Settings>>().Value.DbFile);

    app.UseSerilogRequestLogging();

    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandlerPathFeature =
                context.Features.Get<IExceptionHandlerPathFeature>();

            var ex = exceptionHandlerPathFeature?.Error;

            if (ex != null)
            {
                await context.RequestServices.GetRequiredService<StudentContext>().GenerateIncident("Exception occured", ex);
            }

            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("An error occurred.");
        });
    });

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<StudueContext>();

        db.Database.Migrate();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.Use(async (context, next) =>
    {
        var endpoint = context.GetEndpoint();
        var studentRequired = endpoint?.Metadata.GetMetadata<StudentRequiredAttribute>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        if (studentRequired == null)
        {
            await next(context);
            return;
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            try
            {
                await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
        }

        var studentContext = context.RequestServices.GetRequiredService<StudentContext>();

        var studentId = GetCookieOrQuery(context, "student_id", logger);
        if (studentId == null)
        {
            context.Response.Redirect("/login");
            return;
        }

        var (student, errorMsg) = await studentContext.GetOrCreateStudent(studentId);
        if (student == null)
        {
            context.Response.Redirect($"/login?message={errorMsg}");
            context.Response.Cookies.Delete("student_id");
            return;
        }

        var writeToken = GetCookieOrQuery(context, "write_token", logger);
        if (writeToken != null)
        {
            if (student.IsBanned)
            {
                context.Response.Cookies.Delete("write_token");
            }
            else
            {
                if (writeToken != student.WriteToken)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Cookies.Delete("write_token");
                    return;
                }

                studentContext.HasWriteAccess = true;
            }
        }

        if (studentRequired.RequireWriteAccess && !studentContext.HasWriteAccess)
        {
            if (context.Request.Headers.Accept.Any(x => x != null && x.Contains("text/html")))
                context.Response.Redirect("/");
            else
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            return;
        }

        await next(context);
    });


    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    app.UseStaticFiles();
    app.MapStaticAssets();

    app.UseAuthentication();
    app.UseAuthorization();

    app.UseAntiforgery();

    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    PushService.RegisterEndpoint(app);

    app.MapPost("/settings/refetchSchedule", async (StudentContext studentContext, StudueContext studueContext) =>
        {
            if (!studentContext.HasWriteAccess)
                return Results.Unauthorized();

            var success = await studentContext.FetchModulesForStudent(studentContext.Student);
            if (success)
            {
                await studueContext.SaveChangesAsync();
                return Results.Ok();
            }

            return Results.InternalServerError("Failed to fetch schedule for current semester");
        }).WithMetadata(new StudentRequiredAttribute{ RequireWriteAccess = true});

    app.MapGet("/admin/downloadDb", async (IOptions<Settings> settings) =>
    {
        var backupPath = await BackupService.CreateBackup(settings.Value);
        var stream = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Results.File(stream, "application/octet-stream", Path.GetFileName(backupPath));
    }).WithMetadata(new StudentRequiredAttribute())
        .RequireAuthorization();

    app.MapPost("/assignment/{assignmentId:int}/{completed:bool}", async (int assignmentId, bool completed, StudueContext studueContext, StudentContext studentContext, ILogger<Program> logger) =>
    {
        if (!studentContext.HasWriteAccess)
            return Results.Unauthorized();

        var assignment = await studueContext.Assignements.Include(x => x.CompletedByStudents).FirstOrDefaultAsync(x => x.Id == assignmentId);
        if (assignment == null)
            return Results.NotFound();
        if (completed)
        {
            logger.LogInformation("{0} marked '{1}' as completed", studentContext.Student.StudentId, assignment.Title);
            assignment.CompletedByStudents.Add(studentContext.Student);
        }
        else
        {
            logger.LogInformation("{0} marked '{1}' as not completed", studentContext.Student.StudentId, assignment.Title);
            assignment.CompletedByStudents.Remove(studentContext.Student);
        }
        await studueContext.SaveChangesAsync();

        return Results.Ok();
    }).WithMetadata(new StudentRequiredAttribute { RequireWriteAccess = true });

    app.MapPost("/logout", (HttpContext http) =>
    {
        http.Response.Cookies.Delete("student_id", IdentityCookie());
        http.Response.Cookies.Delete("write_token", IdentityCookie());

        return Results.Ok();
    });

    app.Run();
}
catch (Exception e)
{
    Console.WriteLine(e);
}

CookieOptions IdentityCookie() => new()
{
    MaxAge = TimeSpan.FromDays(365),
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Lax,
    Path = "/",
};

string? GetCookieOrQuery(HttpContext context, string name, ILogger<Program> logger)
{
    if (context.Request.Query.TryGetValue(name, out var queryValue))
    {
        if (queryValue is [{ } str])
        {
            logger.LogInformation("{studentId} just logged in, writing {cookieName} cookie", str, name);

            context.Response.Cookies.Append(name, str, IdentityCookie());
            return str;
        }
    }

    if (context.Request.Cookies.TryGetValue(name, out var cookieWriteToken))
        return cookieWriteToken;

    return null;
}

namespace Studue
{
    public class AssignmentModel
    {
        public string ModuleCode { get; set; } = "";
        public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(Helper.Now());
        public TimeOnly? DueTime { get; set; }
        public string Title { get; set; } = "";
        public string? Details { get; set; }
        public AssignmentType Type { get; set; } = AssignmentType.Mandatory;
    }

    public enum AssignmentType
    {
        Mandatory,
        Optional
    }
}
