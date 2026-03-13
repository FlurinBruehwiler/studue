package ch.studue.auth;

import ch.studue.json.SimpleJson;
import java.math.BigDecimal;
import java.time.Instant;
import java.util.Base64;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Optional;
import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;

public final class SessionService {
    private final byte[] secretKey;
    private final long ttlSeconds;

    public SessionService(String secret, long ttlSeconds) {
        this.secretKey = secret.getBytes(java.nio.charset.StandardCharsets.UTF_8);
        this.ttlSeconds = ttlSeconds;
    }

    public Session create(SessionUser user) {
        Instant now = Instant.now();
        Instant expiresAt = now.plusSeconds(ttlSeconds);
        String token = createSignedToken(user, now, expiresAt);
        return new Session(token, user, now, expiresAt);
    }

    public Optional<Session> find(String token) {
        if (token == null || token.isBlank()) {
            return Optional.empty();
        }

        String[] parts = token.split("\\.", 2);
        if (parts.length != 2) {
            return Optional.empty();
        }

        if (!constantTimeEquals(parts[1], sign(parts[0]))) {
            return Optional.empty();
        }

        Map<String, Object> payload = parsePayload(parts[0]);
        Instant createdAt = instantValue(payload.get("createdAt"));
        Instant expiresAt = instantValue(payload.get("expiresAt"));
        if (createdAt == null || expiresAt == null || expiresAt.isBefore(Instant.now())) {
            return Optional.empty();
        }

        SessionUser user = new SessionUser(
                stringValue(payload.get("githubLogin")),
                stringValue(payload.get("displayName")),
                stringValue(payload.get("email")),
                Boolean.TRUE.equals(payload.get("isAllowedEditor"))
        );
        return Optional.of(new Session(token, user, createdAt, expiresAt));
    }

    public void destroy(String sessionId) {
    }

    public long ttlSeconds() {
        return ttlSeconds;
    }

    private String createSignedToken(SessionUser user, Instant createdAt, Instant expiresAt) {
        Map<String, Object> payload = new LinkedHashMap<>();
        payload.put("githubLogin", user.githubLogin());
        payload.put("displayName", user.displayName());
        payload.put("email", user.email());
        payload.put("isAllowedEditor", user.isAllowedEditor());
        payload.put("createdAt", createdAt.getEpochSecond());
        payload.put("expiresAt", expiresAt.getEpochSecond());

        String encodedPayload = Base64.getUrlEncoder()
                .withoutPadding()
                .encodeToString(SimpleJson.stringify(payload).getBytes(java.nio.charset.StandardCharsets.UTF_8));
        return encodedPayload + "." + sign(encodedPayload);
    }

    private Map<String, Object> parsePayload(String encodedPayload) {
        byte[] decodedBytes = Base64.getUrlDecoder().decode(encodedPayload);
        @SuppressWarnings("unchecked")
        Map<String, Object> payload = (Map<String, Object>) SimpleJson.parseObject(new String(decodedBytes, java.nio.charset.StandardCharsets.UTF_8));
        return payload;
    }

    private String sign(String encodedPayload) {
        try {
            Mac mac = Mac.getInstance("HmacSHA256");
            mac.init(new SecretKeySpec(secretKey, "HmacSHA256"));
            return Base64.getUrlEncoder().withoutPadding().encodeToString(
                    mac.doFinal(encodedPayload.getBytes(java.nio.charset.StandardCharsets.UTF_8))
            );
        } catch (Exception exception) {
            throw new IllegalStateException("Could not sign session token.", exception);
        }
    }

    private boolean constantTimeEquals(String left, String right) {
        byte[] leftBytes = left.getBytes(java.nio.charset.StandardCharsets.UTF_8);
        byte[] rightBytes = right.getBytes(java.nio.charset.StandardCharsets.UTF_8);
        if (leftBytes.length != rightBytes.length) {
            return false;
        }

        int result = 0;
        for (int index = 0; index < leftBytes.length; index++) {
            result |= leftBytes[index] ^ rightBytes[index];
        }
        return result == 0;
    }

    private Instant instantValue(Object value) {
        if (value instanceof BigDecimal decimal) {
            return Instant.ofEpochSecond(decimal.longValue());
        }
        return null;
    }

    private String stringValue(Object value) {
        return value == null ? "" : String.valueOf(value);
    }
}
