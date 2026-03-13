package ch.studue.auth;

import java.time.Instant;

public record Session(
        String sessionId,
        SessionUser user,
        Instant createdAt,
        Instant expiresAt
) {
    public boolean isExpired(Instant now) {
        return expiresAt.isBefore(now);
    }
}
