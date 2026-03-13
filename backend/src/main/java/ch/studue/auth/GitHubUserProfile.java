package ch.studue.auth;

public record GitHubUserProfile(
        String login,
        String displayName,
        String email
) {
}
