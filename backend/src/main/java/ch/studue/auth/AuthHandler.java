package ch.studue.auth;

import ch.studue.config.AppConfig;
import ch.studue.http.HttpExchangeHelper;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import java.io.IOException;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.Optional;

public final class AuthHandler implements HttpHandler {
    private final AppConfig config;
    private final SessionService sessionService;
    private final AuthorizationService authorizationService;

    public AuthHandler(AppConfig config, SessionService sessionService, AuthorizationService authorizationService) {
        this.config = config;
        this.sessionService = sessionService;
        this.authorizationService = authorizationService;
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
            handleCallbackPlaceholder(exchange);
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
        String callbackUrl = config.appBaseUrl() + "/api/auth/callback";
        String redirectUrl = config.githubOauthBaseUrl()
                + "/authorize?client_id=" + encode(config.githubClientId())
                + "&redirect_uri=" + encode(callbackUrl)
                + "&scope=" + encode("read:user user:email");
        HttpExchangeHelper.redirect(exchange, redirectUrl);
    }

    private void handleCallbackPlaceholder(HttpExchange exchange) throws IOException {
        boolean isAllowed = authorizationService.isAllowedEditor("jane.doe@students.zhaw.ch");
        Session session = sessionService.create(new SessionUser(
                "jdoe",
                "Jane Doe",
                "jane.doe@students.zhaw.ch",
                isAllowed
        ));
        HttpExchangeHelper.setSessionCookie(exchange, session.sessionId());
        HttpExchangeHelper.redirect(exchange, "/");
    }

    private void handleLogout(HttpExchange exchange) throws IOException {
        HttpExchangeHelper.findSessionId(exchange).ifPresent(sessionService::destroy);
        HttpExchangeHelper.clearSessionCookie(exchange);
        HttpExchangeHelper.sendJson(exchange, 200, Map.of("ok", true));
    }

    private String encode(String value) {
        return URLEncoder.encode(value, StandardCharsets.UTF_8);
    }
}
