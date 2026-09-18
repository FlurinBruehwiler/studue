using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Studue.Services;

namespace Studue.MCP;

public class McpAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    StudentContext studentContext,
    ILogger<McpAuthenticationHandler> log
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Mcp";
    private const string StudentIdHeader = "X-Student-Id";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var writeToken = GetBearerToken();
        var studentId = Request.Headers[StudentIdHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(writeToken) || string.IsNullOrWhiteSpace(studentId))
            return AuthenticateResult.NoResult();

        var (student, errorMessage) = await studentContext.GetOrCreateStudent(studentId);
        if (student == null)
            return AuthenticateResult.Fail(errorMessage);

        if (student.IsBanned)
            return AuthenticateResult.Fail("This student is banned");

        if (!TokenMatches(writeToken, student.WriteToken))
        {
            log.LogWarning(
                "MCP request for {studentId} sent an invalid write token",
                student.StudentId
            );
            return AuthenticateResult.Fail("Invalid write token");
        }

        studentContext.HasWriteAccess = true;

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, student.StudentId)], SchemeName)
        );

        return AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = $"Bearer realm=\"{Scheme.Name}\"";
        return base.HandleChallengeAsync(properties);
    }

    private string? GetBearerToken()
    {
        var header = Request.Headers.Authorization.FirstOrDefault();

        return header?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
            ? header["Bearer ".Length..].Trim()
            : null;
    }

    private static bool TokenMatches(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected)
        );
}
