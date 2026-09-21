using System.IO.Compression;
using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Studue;
using Studue.Components;
using Studue.MCP;
using Studue.Services;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog(
        (context, services, config) =>
        {
            config.ReadFrom.Configuration(context.Configuration);
            config.ReadFrom.Services(services);
        }
    );

    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<ILogEventEnricher, StudentLogEnricher>();

    builder.Services.Configure<Settings>(builder.Configuration.GetSection("Studue"));
    builder.Services.PostConfigure<Settings>(settings =>
    {
        settings.DbFile = Path.GetFullPath(settings.DbFile, builder.Environment.ContentRootPath);
        settings.DatabaseBackupDir = Path.GetFullPath(
            settings.DatabaseBackupDir,
            builder.Environment.ContentRootPath
        );
    });
    builder.Services.AddScoped<StudentContext>();
    builder.Services.AddScoped<AssignmentService>();
    builder.Services.AddDbContextFactory<StudueContext>(
        (services, options) =>
        {
            options.UseSqlite(
                BackupService.GetSqliteConnectionString(
                    services.GetRequiredService<IOptions<Settings>>().Value
                ),
                o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
            );
        }
    );

    builder.Services.AddHostedService<BackupService>();
    builder.Services.AddHttpClient();

    var authentication = builder
        .Services.AddAuthentication(AdminAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, AdminAuthenticationHandler>(
            AdminAuthenticationHandler.SchemeName,
            _ => { }
        );
    StudueMcp.RegisterAuthentication(authentication);
    // caddy terminates tls and proxies over plain http, so without this the app believes
    // every request is http: canonical urls come out http, hsts is skipped and the https
    // redirect cannot find a port to redirect to
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders =
            ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
    });

    builder.Services.AddAuthorization(options =>
    {
        // the default policy authenticates against the Admin scheme; mcp clients carry headers
        // instead of the cookies that scheme depends on, so they get their own scheme and policy
        options.AddPolicy(
            StudueMcp.AuthorizationPolicy,
            policy =>
                policy
                    .AddAuthenticationSchemes(McpAuthenticationHandler.SchemeName)
                    .RequireAuthenticatedUser()
        );
    });

    StudueMcp.RegisterServices(builder.Services);

    builder.Services.AddResponseCompression(options => options.EnableForHttps = true);
    builder.Services.Configure<BrotliCompressionProviderOptions>(o =>
        o.Level = CompressionLevel.Fastest
    );
    builder.Services.Configure<GzipCompressionProviderOptions>(o =>
        o.Level = CompressionLevel.Fastest
    );

    builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

    builder.Services.AddRazorComponents().AddInteractiveServerComponents();

    builder.Services.AddHostedService<PushService>();

    var app = builder.Build();

    Log.Information(
        "Using SQLite database at {DbFile}",
        app.Services.GetRequiredService<IOptions<Settings>>().Value.DbFile
    );

    app.UseForwardedHeaders();

    app.UseSerilogRequestLogging();

    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();

            var ex = exceptionHandlerPathFeature?.Error;

            if (ex != null)
            {
                await context
                    .RequestServices.GetRequiredService<StudentContext>()
                    .GenerateIncident("Exception occured", ex);
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
        // before anything that writes a body
        app.UseResponseCompression();

        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.Use(
        async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            var studentRequired = endpoint?.Metadata.GetMetadata<StudentRequiredAttribute>();
            var studentOptional = endpoint?.Metadata.GetMetadata<StudentOptionalAttribute>();
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

            if (studentRequired == null && studentOptional == null)
            {
                await next(context);
                return;
            }

            var studentContext = context.RequestServices.GetRequiredService<StudentContext>();

            var studentId = GetCookieOrQuery(context, "student_id", logger);
            if (studentId == null)
            {
                if (studentOptional != null)
                {
                    await next(context);
                    return;
                }

                var wanted = $"{context.Request.Path}{context.Request.QueryString}";
                context.Response.Redirect($"/login?next={Uri.EscapeDataString(wanted)}");
                return;
            }

            var (student, errorMsg) = await studentContext.GetOrCreateStudent(studentId);
            if (student == null)
            {
                context.Response.Cookies.Delete("student_id");

                if (studentOptional != null)
                {
                    await next(context);
                    return;
                }

                context.Response.Redirect($"/login?message={errorMsg}");
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

            if (studentRequired is { RequireWriteAccess: true } && !studentContext.HasWriteAccess)
            {
                if (context.Request.Headers.Accept.Any(x => x != null && x.Contains("text/html")))
                    context.Response.Redirect("/");
                else
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                return;
            }

            await next(context);
        }
    );

    // re-executing into the html not-found page would replace the json body and the
    // WWW-Authenticate header an mcp client needs to understand a 401
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments(StudueMcp.RoutePrefix),
        branch =>
            branch.UseStatusCodePagesWithReExecute(
                "/not-found",
                createScopeForStatusCodePages: true
            )
    );
    app.UseHttpsRedirection();

    app.UseStaticFiles();
    app.MapStaticAssets();

    app.UseAuthentication();
    app.UseAuthorization();

    // after UseAuthentication: antiforgery tokens are bound to the authenticated user, so
    // validating before the user is resolved rejects every token issued to an admin
    app.Use(
        async (context, next) =>
        {
            var endpoint = context.GetEndpoint();
            var gated =
                endpoint?.Metadata.GetMetadata<StudentRequiredAttribute>() != null
                || endpoint?.Metadata.GetMetadata<StudentOptionalAttribute>() != null;

            if (
                gated
                && !HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method)
            )
            {
                try
                {
                    await context
                        .RequestServices.GetRequiredService<IAntiforgery>()
                        .ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException error)
                {
                    context
                        .RequestServices.GetRequiredService<ILogger<Program>>()
                        .LogWarning(
                            "Antiforgery rejected {method} {path}: {reason}",
                            context.Request.Method,
                            context.Request.Path,
                            error.Message
                        );

                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
            }

            await next(context);
        }
    );

    app.UseAntiforgery();

    app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

    PushService.RegisterEndpoint(app);
    StudueMcp.RegisterEndpoint(app);

    app.MapPost(
            "/settings/refetchSchedule",
            async (StudentContext studentContext, StudueContext studueContext) =>
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
            }
        )
        .WithMetadata(new StudentRequiredAttribute { RequireWriteAccess = true });

    app.MapGet(
            "/admin/downloadDb",
            async (IOptions<Settings> settings) =>
            {
                var backupPath = await BackupService.CreateBackup(settings.Value);
                var stream = new FileStream(
                    backupPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                );
                return Results.File(
                    stream,
                    "application/octet-stream",
                    Path.GetFileName(backupPath)
                );
            }
        )
        .WithMetadata(new StudentRequiredAttribute())
        .RequireAuthorization();

    app.MapPost(
            "/assignment/{assignmentId:int}/{completed:bool}",
            async (
                int assignmentId,
                bool completed,
                StudueContext studueContext,
                StudentContext studentContext,
                ILogger<Program> logger
            ) =>
            {
                if (!studentContext.HasWriteAccess)
                    return Results.Unauthorized();

                var assignment = await studueContext
                    .Assignements.Include(x => x.CompletedByStudents)
                    .FirstOrDefaultAsync(x => x.Id == assignmentId);
                if (assignment == null)
                    return Results.NotFound();
                if (completed)
                {
                    logger.LogInformation(
                        "{0} marked '{1}' as completed",
                        studentContext.Student.StudentId,
                        assignment.Title
                    );
                    assignment.CompletedByStudents.Add(studentContext.Student);
                }
                else
                {
                    logger.LogInformation(
                        "{0} marked '{1}' as not completed",
                        studentContext.Student.StudentId,
                        assignment.Title
                    );
                    assignment.CompletedByStudents.Remove(studentContext.Student);
                }
                await studueContext.SaveChangesAsync();

                return Results.Ok();
            }
        )
        .WithMetadata(new StudentRequiredAttribute { RequireWriteAccess = true });

    // no write access required: a student who has not authenticated to edit still sees banners,
    // so they must be able to get rid of them
    app.MapPost(
            "/banner/{bannerId:int}/dismiss",
            async (int bannerId, StudueContext studueContext, StudentContext studentContext) =>
            {
                var studentId = studentContext.Student.Id;

                var banner = await studueContext.Banners.FirstOrDefaultAsync(x => x.Id == bannerId);
                if (banner == null)
                    return Results.NotFound();

                await studueContext
                    .Entry(banner)
                    .Collection(x => x.DismissedByStudents)
                    .Query()
                    .Where(x => x.Id == studentId)
                    .LoadAsync();

                if (banner.DismissedByStudents.Count != 0)
                    return Results.Ok();

                banner.DismissedByStudents.Add(studentContext.Student);

                try
                {
                    await studueContext.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    var dismissed = await studueContext.Banners.AnyAsync(x =>
                        x.Id == bannerId && x.DismissedByStudents.Any(s => s.Id == studentId)
                    );
                    if (!dismissed)
                        throw;
                }

                return Results.Ok();
            }
        )
        .WithMetadata(new StudentRequiredAttribute());

    app.MapPost(
        "/logout",
        (HttpContext http) =>
        {
            http.Response.Cookies.Delete("student_id", IdentityCookie());
            http.Response.Cookies.Delete("write_token", IdentityCookie());

            return Results.Ok();
        }
    );

    app.MapPost(
            "/rotateToken",
            async (HttpContext http, StudentContext studentContext, StudueContext studueContext) =>
            {
                http.Response.Cookies.Delete("student_id", IdentityCookie());
                http.Response.Cookies.Delete("write_token", IdentityCookie());

                studentContext.Student.WriteToken = StudentContext.GenerateWriteToken();
                await studueContext.SaveChangesAsync();

                return Results.Ok();
            }
        )
        .WithMetadata(new StudentRequiredAttribute { RequireWriteAccess = true });

    app.MapGet(
        "/sitemap.xml",
        (HttpContext http) =>
        {
            var origin = $"{http.Request.Scheme}://{http.Request.Host}";

            return Results.Text(
                $"""<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"><url><loc>{origin}/</loc></url></urlset>""",
                "application/xml"
            );
        }
    );

    app.Run();
}
catch (Exception e)
{
    Console.WriteLine(e);
}

CookieOptions IdentityCookie() =>
    new()
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
            logger.LogInformation(
                "{studentId} just logged in, writing {cookieName} cookie",
                str,
                name
            );

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
        Optional,
    }
}
