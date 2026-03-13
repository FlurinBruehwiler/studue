package ch.studue.auth;

import ch.studue.config.AppConfig;
import ch.studue.http.HttpExchangeHelper;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import java.io.IOException;
import java.util.Map;
import java.util.Optional;

public final class AuthHandler implements HttpHandler {
    private final AppConfig config;
    private final SessionService sessionService;
    private final AuthorizationService authorizationService;
    private final OAuthStateService oAuthStateService;
    private final GitHubOAuthService gitHubOAuthService;

    public AuthHandler(
            AppConfig config,
            SessionService sessionService,
            AuthorizationService authorizationService,
            OAuthStateService oAuthStateService,
            GitHubOAuthService gitHubOAuthService
    ) {
        this.config = config;
        this.sessionService = sessionService;
        this.authorizationService = authorizationService;
        this.oAuthStateService = oAuthStateService;
        this.gitHubOAuthService = gitHubOAuthService;
    }

    @Override
    public void handle(HttpExchange exchange) throws IOException {
        String path = exchange.getRequestURI().getPath();
        String method = exchange.getRequestMethod();

        if (path.equals("/api/auth/me") && method.equalsIgnoreCase("GET")) {
            handleMe(exchange);
            return;
        }

        if (path.equals("/api/auth/login") && method.equalsIgnoreCase("GET")) {
            handleLogin(exchange);
            return;
        }

        if (path.equals("/api/auth/callback") && method.equalsIgnoreCase("GET")) {
            handleCallback(exchange);
            return;
        }

        if (path.equals("/api/auth/logout") && method.equalsIgnoreCase("POST")) {
            handleLogout(exchange);
            return;
        }

        HttpExchangeHelper.sendJson(exchange, 404, Map.of(
                "error", Map.of(
                        "code", "not_found",
                        "message", "Auth endpoint not found."
                )
        ));
    }

    private void handleMe(HttpExchange exchange) throws IOException {
        Optional<Session> session = HttpExchangeHelper.findSession(exchange, sessionService);
        if (session.isEmpty()) {
            Map<String, Object> body = new java.util.LinkedHashMap<>();
            body.put("authenticated", false);
            body.put("user", null);
            HttpExchangeHelper.sendJson(exchange, 200, body);
            return;
        }

        SessionUser user = session.get().user();
        HttpExchangeHelper.sendJson(exchange, 200, Map.of(
                "authenticated", true,
                "user", Map.of(
                        "githubLogin", user.githubLogin(),
                        "displayName", user.displayName(),
                        "email", user.email(),
                        "isAllowedEditor", user.isAllowedEditor()
                )
        ));
    }

    private void handleLogin(HttpExchange exchange) throws IOException {
        String state = oAuthStateService.issue();
        HttpExchangeHelper.redirect(exchange, gitHubOAuthService.buildAuthorizeUrl(state));
    }

    private void handleCallback(HttpExchange exchange) throws IOException {
        Map<String, String> params = HttpExchangeHelper.queryParams(exchange);
        String error = params.get("error");
        String state = params.get("state");
        String code = params.get("code");

        if (error != null && !error.isBlank()) {
            HttpExchangeHelper.redirect(exchange, config.frontendBaseUrl() + "?auth=error");
            return;
        }

        if (!oAuthStateService.consume(state)) {
            HttpExchangeHelper.redirect(exchange, config.frontendBaseUrl() + "?auth=invalid_state");
            return;
        }

        if (code == null || code.isBlank()) {
            HttpExchangeHelper.redirect(exchange, config.frontendBaseUrl() + "?auth=missing_code");
            return;
        }

        try {
            GitHubUserProfile profile = gitHubOAuthService.authenticate(code);
            boolean isAllowed = authorizationService.isAllowedEditor(profile.login());

            if (!isAllowed) {
                System.out.println("OAuth login rejected for login: " + profile.login() + " (email: " + profile.email() + ")");
                HttpExchangeHelper.redirect(exchange, config.frontendBaseUrl() + "?auth=forbidden");
                return;
            }

            Session session = sessionService.create(new SessionUser(
                    profile.login(),
                    profile.displayName(),
                    profile.email(),
                    true
            ));
            HttpExchangeHelper.setSessionCookie(
                    exchange,
                    session.sessionId(),
                    config.appBaseUrl().startsWith("https://"),
                    sessionService.ttlSeconds()
            );
            HttpExchangeHelper.redirect(exchange, config.frontendBaseUrl());
        } catch (IOException exception) {
            HttpExchangeHelper.redirect(exchange, config.frontendBaseUrl() + "?auth=oauth_failed");
        }
    }

    private void handleLogout(HttpExchange exchange) throws IOException {
        HttpExchangeHelper.findSessionId(exchange).ifPresent(sessionService::destroy);
        HttpExchangeHelper.clearSessionCookie(exchange, config.appBaseUrl().startsWith("https://"));
        HttpExchangeHelper.sendJson(exchange, 200, Map.of("ok", true));
    }
}
