using Microsoft.AspNetCore.Authentication;
using ModelContextProtocol.AspNetCore;

namespace Studue.MCP;

public static class StudueMcp
{
    public const string RoutePrefix = "/mcp";
    public const string AuthorizationPolicy = "McpClient";

    public static void RegisterServices(IServiceCollection services)
    {
        services
            .AddMcpServer()
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<StudueMcpTools>();
    }

    public static void RegisterAuthentication(AuthenticationBuilder authentication)
    {
        authentication.AddScheme<AuthenticationSchemeOptions, McpAuthenticationHandler>(
            McpAuthenticationHandler.SchemeName,
            _ => { }
        );
    }

    public static void RegisterEndpoint(WebApplication webApplication)
    {
        webApplication.MapMcp(RoutePrefix).RequireAuthorization(AuthorizationPolicy);
    }
}
