package ch.studue.auth;

import ch.studue.audit.AuditLogStore;
import ch.studue.http.HttpExchangeHelper;
import ch.studue.json.SimpleJson;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import java.io.IOException;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.Optional;

public final class AdminHandler implements HttpHandler {
    private final SessionService sessionService;
    private final AuthorizationService authorizationService;
    private final AuditLogStore auditLogStore;

    public AdminHandler(SessionService sessionService, AuthorizationService authorizationService, AuditLogStore auditLogStore) {
        this.sessionService = sessionService;
        this.authorizationService = authorizationService;
        this.auditLogStore = auditLogStore;
    }

    @Override
    public void handle(HttpExchange exchange) throws IOException {
        Optional<Session> session = HttpExchangeHelper.findSession(exchange, sessionService);
        if (session.isEmpty() || !authorizationService.isAdmin(session.get().user().githubLogin())) {
            HttpExchangeHelper.sendJson(exchange, 403, Map.of(
                    "error", Map.of(
                            "code", "forbidden",
                            "message", "You must be an admin to access this page."
                    )
            ));
            return;
        }

        String path = exchange.getRequestURI().getPath();
        String method = exchange.getRequestMethod();

        if (path.equals("/api/admin/access-control") && method.equalsIgnoreCase("GET")) {
            AccessControl accessControl = authorizationService.getAccessControl();
            HttpExchangeHelper.sendJson(exchange, 200, Map.of(
                    "admins", accessControl.admins(),
                    "editors", accessControl.editors()
            ));
            return;
        }

        if (path.equals("/api/admin/editors") && method.equalsIgnoreCase("POST")) {
            @SuppressWarnings("unchecked")
            Map<String, Object> body = (Map<String, Object>) SimpleJson.parseObject(HttpExchangeHelper.readBody(exchange));
            String githubLogin = String.valueOf(body.getOrDefault("githubLogin", "")).trim();
            if (githubLogin.isBlank()) {
                HttpExchangeHelper.badRequest(exchange, "validation_error", "The githubLogin field is required.", Map.of("field", "githubLogin"));
                return;
            }

            authorizationService.addEditor(githubLogin);
            AccessControl accessControl = authorizationService.getAccessControl();
            HttpExchangeHelper.sendJson(exchange, 200, Map.of(
                    "admins", accessControl.admins(),
                    "editors", accessControl.editors()
            ));
            return;
        }

        if (path.startsWith("/api/admin/editors/") && method.equalsIgnoreCase("DELETE")) {
            String githubLogin = decodePathSegment(path.substring("/api/admin/editors/".length()));
            if (githubLogin.isBlank()) {
                HttpExchangeHelper.notFound(exchange, "Whitelist entry not found.");
                return;
            }

            authorizationService.removeEditor(githubLogin);
            AccessControl accessControl = authorizationService.getAccessControl();
            HttpExchangeHelper.sendJson(exchange, 200, Map.of(
                    "admins", accessControl.admins(),
                    "editors", accessControl.editors()
            ));
            return;
        }

        if (path.equals("/api/admin/admins") && method.equalsIgnoreCase("POST")) {
            @SuppressWarnings("unchecked")
            Map<String, Object> body = (Map<String, Object>) SimpleJson.parseObject(HttpExchangeHelper.readBody(exchange));
            String githubLogin = String.valueOf(body.getOrDefault("githubLogin", "")).trim();
            if (githubLogin.isBlank()) {
                HttpExchangeHelper.badRequest(exchange, "validation_error", "The githubLogin field is required.", Map.of("field", "githubLogin"));
                return;
            }

            authorizationService.addAdmin(githubLogin);
            AccessControl accessControl = authorizationService.getAccessControl();
            HttpExchangeHelper.sendJson(exchange, 200, Map.of(
                    "admins", accessControl.admins(),
                    "editors", accessControl.editors()
            ));
            return;
        }

        if (path.equals("/api/admin/logs") && method.equalsIgnoreCase("GET")) {
            HttpExchangeHelper.sendJson(exchange, 200, Map.of(
                    "items", auditLogStore.readAll().stream().map(entry -> Map.of(
                            "timestamp", entry.timestamp(),
                            "action", entry.action(),
                            "actorLogin", entry.actorLogin(),
                            "actorDisplayName", entry.actorDisplayName(),
                            "assignmentId", entry.assignmentId(),
                            "title", entry.title(),
                            "dueDate", entry.dueDate(),
                            "dueTime", entry.dueTime()
                    )).toList()
            ));
            return;
        }

        HttpExchangeHelper.sendJson(exchange, 404, Map.of(
                "error", Map.of(
                        "code", "not_found",
                        "message", "Admin endpoint not found."
                )
        ));
    }

    private String decodePathSegment(String value) {
        return URLDecoder.decode(value, StandardCharsets.UTF_8);
    }
}
