package ch.studue.auth;

import java.security.SecureRandom;
import java.time.Instant;
import java.util.Base64;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.ConcurrentHashMap;

public final class SessionService {
    private final Map<String, Session> sessions = new ConcurrentHashMap<>();
    private final SecureRandom secureRandom = new SecureRandom();
    private final long ttlSeconds;

    public SessionService(long ttlSeconds) {
        this.ttlSeconds = ttlSeconds;
    }

    public Session create(SessionUser user) {
        cleanupExpired();

        Instant now = Instant.now();
        String sessionId = randomToken();
        Session session = new Session(sessionId, user, now, now.plusSeconds(ttlSeconds));
        sessions.put(sessionId, session);
        return session;
    }

    public Optional<Session> find(String sessionId) {
        if (sessionId == null || sessionId.isBlank()) {
            return Optional.empty();
        }

        Session session = sessions.get(sessionId);
        if (session == null) {
            return Optional.empty();
        }

        if (session.isExpired(Instant.now())) {
            sessions.remove(sessionId);
            return Optional.empty();
        }

        return Optional.of(session);
    }

    public void destroy(String sessionId) {
        if (sessionId != null) {
            sessions.remove(sessionId);
        }
    }

    private void cleanupExpired() {
        Instant now = Instant.now();
        sessions.entrySet().removeIf(entry -> entry.getValue().isExpired(now));
    }

    private String randomToken() {
        byte[] bytes = new byte[24];
        secureRandom.nextBytes(bytes);
        return Base64.getUrlEncoder().withoutPadding().encodeToString(bytes);
    }
}
