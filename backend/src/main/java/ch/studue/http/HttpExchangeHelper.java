package ch.studue.http;

import ch.studue.auth.Session;
import ch.studue.auth.SessionService;
import ch.studue.auth.SessionUser;
import ch.studue.config.AppConfig;
import ch.studue.json.SimpleJson;
import com.sun.net.httpserver.HttpExchange;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpCookie;
import java.net.URLDecoder;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Optional;

public final class HttpExchangeHelper {
    private static final String SESSION_COOKIE = "studue_session";

    private HttpExchangeHelper() {
    }

    public static void sendJson(HttpExchange exchange, int status, Object body) throws IOException {
        byte[] bytes = SimpleJson.stringify(body).getBytes(StandardCharsets.UTF_8);
        exchange.getResponseHeaders().set("Content-Type", "application/json; charset=utf-8");
        exchange.sendResponseHeaders(status, bytes.length);

        try (OutputStream outputStream = exchange.getResponseBody()) {
            outputStream.write(bytes);
        }
    }

    public static void redirect(HttpExchange exchange, String location) throws IOException {
        exchange.getResponseHeaders().set("Location", location);
        exchange.sendResponseHeaders(302, -1);
        exchange.close();
    }

    public static String readBody(HttpExchange exchange) throws IOException {
        try (InputStream inputStream = exchange.getRequestBody()) {
            return new String(inputStream.readAllBytes(), StandardCharsets.UTF_8);
        }
    }

    public static Map<String, String> queryParams(HttpExchange exchange) {
        Map<String, String> params = new LinkedHashMap<>();
        String query = exchange.getRequestURI().getQuery();
        if (query == null || query.isBlank()) {
            return params;
        }

        Arrays.stream(query.split("&"))
                .map(pair -> pair.split("=", 2))
                .forEach(parts -> params.put(decode(parts[0]), parts.length > 1 ? decode(parts[1]) : ""));
        return params;
    }

    public static Optional<Session> findSession(HttpExchange exchange, SessionService sessionService) {
        return findSessionId(exchange).flatMap(sessionService::find);
    }

    public static Optional<Session> findSession(HttpExchange exchange, SessionService sessionService, AppConfig config) {
        Optional<Session> session = findSession(exchange, sessionService);
        if (session.isPresent()) {
            return session;
        }

        if (!config.authBypassEnabled()) {
            return Optional.empty();
        }

        Instant now = Instant.now();
        Instant expiresAt = now.plusSeconds(sessionService.ttlSeconds());
        return Optional.of(new Session(
                "auth-bypass",
                new SessionUser(
                        config.authBypassGithubLogin(),
                        config.authBypassDisplayName(),
                        config.authBypassEmail(),
                        true,
                        true
                ),
                now,
                expiresAt
        ));
    }

    public static Optional<String> findSessionId(HttpExchange exchange) {
        String cookieHeader = exchange.getRequestHeaders().getFirst("Cookie");
        if (cookieHeader == null || cookieHeader.isBlank()) {
            return Optional.empty();
        }

        return HttpCookie.parse(cookieHeader).stream()
                .filter(cookie -> SESSION_COOKIE.equals(cookie.getName()))
                .map(HttpCookie::getValue)
                .findFirst();
    }

    public static void setSessionCookie(HttpExchange exchange, String sessionId, boolean secure, long maxAgeSeconds) {
        exchange.getResponseHeaders().add(
                "Set-Cookie",
                SESSION_COOKIE + "=" + sessionId + "; Path=/; HttpOnly; SameSite=Lax; Max-Age=" + maxAgeSeconds + (secure ? "; Secure" : "")
        );
    }

    public static void clearSessionCookie(HttpExchange exchange, boolean secure) {
        exchange.getResponseHeaders().add(
                "Set-Cookie",
                SESSION_COOKIE + "=; Path=/; HttpOnly; Max-Age=0; SameSite=Lax" + (secure ? "; Secure" : "")
        );
    }

    public static void notFound(HttpExchange exchange, String message) throws IOException {
        sendJson(exchange, 404, Map.of(
                "error", Map.of(
                        "code", "not_found",
                        "message", message
                )
        ));
    }

    public static void badRequest(HttpExchange exchange, String code, String message, Map<String, Object> details) throws IOException {
        sendJson(exchange, 400, Map.of(
                "error", Map.of(
                        "code", code,
                        "message", message,
                        "details", details
                )
        ));
    }

    private static String decode(String value) {
        return URLDecoder.decode(value, StandardCharsets.UTF_8);
    }
}
