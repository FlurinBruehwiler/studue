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

    var dbFile = builder.Configuration["Studue:DbFile"]
        ?? throw new InvalidOperationException("Missing configuration value 'Studue:DbFile'.");

    builder.Services.Configure<Settings>(builder.Configuration.GetSection("Studue"));
    builder.Services.AddScoped<StudentContext>();
    builder.Services.AddDbContextFactory<StudueContext>(options =>
        options.UseSqlite($"Data Source={dbFile}"));

    builder.Services.AddHttpClient();

    builder.Services.AddAuthentication(AdminAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, AdminAuthenticationHandler>(AdminAuthenticationHandler.SchemeName, _ => { });
    builder.Services.AddAuthorization();

    // Add services to the container.
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
            //todo maybe redirect to root and display error??
        });
    });

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<StudueContext>();

        db.Database.Migrate();
    }

    // Configure the HTTP request pipeline.
    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    app.Use(async (context, next) =>
    {
        var endpoint = context.GetEndpoint();
        var studentNotRequired = endpoint?.Metadata.GetMetadata<StudentRequiredAttribute>();

        if (studentNotRequired == null)
        {
            await next(context);
            return;
        }

        var studentContext = context.RequestServices.GetRequiredService<StudentContext>();

        var studentId = GetCookieOrQuery(context, "student_id");
        if (studentId == null)
        {
            context.Response.Redirect("/login");
            return;
        }

        var student = await studentContext.GetOrCreateStudent(studentId);
        if (student == null)
        {
            context.Response.Redirect("/login");
            context.Response.Cookies.Delete("student_id");
            return;
        }

        var writeToken = GetCookieOrQuery(context, "write_token");
        if (writeToken != null)
        {
            if (writeToken != student.WriteToken)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Cookies.Delete("write_token");
                return;
            }

            studentContext.HasWriteAccess = true;
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

string? GetCookieOrQuery(HttpContext context, string name)
{
    string? value = null;
    if (context.Request.Query.TryGetValue(name, out var queryValue))
    {
        if (queryValue is [{ } str])
        {
            value = str;
            context.Response.Cookies.Append(name, str);
        }
    }

    if (context.Request.Cookies.TryGetValue(name, out var cookieWriteToken))
        value = cookieWriteToken;

    return value;
}

namespace Studue
{
    public class AssignmentModel
    {
        public string ModuleCode { get; set; } = "";
        public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.Now);
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
