package ch.studue.auth;

import java.security.SecureRandom;
import java.time.Instant;
import java.util.Base64;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

public final class OAuthStateService {
    private final SecureRandom secureRandom = new SecureRandom();
    private final Map<String, Instant> states = new ConcurrentHashMap<>();
    private final long ttlSeconds;

    public OAuthStateService(long ttlSeconds) {
        this.ttlSeconds = ttlSeconds;
    }

    public String issue() {
        cleanupExpired();
        String state = randomToken();
        states.put(state, Instant.now().plusSeconds(ttlSeconds));
        return state;
    }

    public boolean consume(String state) {
        if (state == null || state.isBlank()) {
            return false;
        }

        Instant expiresAt = states.remove(state);
        return expiresAt != null && expiresAt.isAfter(Instant.now());
    }

    private void cleanupExpired() {
        Instant now = Instant.now();
        states.entrySet().removeIf(entry -> entry.getValue().isBefore(now));
    }

    private String randomToken() {
        byte[] bytes = new byte[24];
        secureRandom.nextBytes(bytes);
        return Base64.getUrlEncoder().withoutPadding().encodeToString(bytes);
    }
}
