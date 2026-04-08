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
    builder.Services.AddScoped<StudentContext>();
    builder.Services.AddDbContextFactory<StudueContext>((services, options) =>
    {
        options.UseSqlite($"Data Source={services.GetRequiredService<IOptions<Settings>>().Value.DbFile}");
    });

    builder.Services.AddHttpClient();

    builder.Services.AddAuthentication(AdminAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, AdminAuthenticationHandler>(AdminAuthenticationHandler.SchemeName, _ => { });
    builder.Services.AddAuthorization();

    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    var app = builder.Build();

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
        var studentNotRequired = endpoint?.Metadata.GetMetadata<StudentRequiredAttribute>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

        if (studentNotRequired == null)
        {
            await next(context);
            return;
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

    app.Run();
}
catch (Exception e)
{
    Console.WriteLine(e);
}
string? GetCookieOrQuery(HttpContext context, string name, ILogger<Program> logger)
{
    if (context.Request.Query.TryGetValue(name, out var queryValue))
    {
        if (queryValue is [{ } str])
        {
            logger.LogInformation("{studentId} just logged in, writing {cookieName} cookie", str, name);

            context.Response.Cookies.Append(name, str, new CookieOptions
            {
                MaxAge = TimeSpan.FromDays(365)
            });
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
