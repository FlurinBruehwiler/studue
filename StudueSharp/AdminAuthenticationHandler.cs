using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using StudueSharp.Services;

namespace StudueSharp;

public class AdminAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    StudentContext studentContext) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Admin";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (studentContext.Student is not { IsAdmin: true } student || !studentContext.HasWriteAccess)
            return Task.FromResult(AuthenticateResult.NoResult());

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, student.StudentId)
        ], SchemeName));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
