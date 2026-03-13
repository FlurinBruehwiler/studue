package ch.studue.auth;

public record SessionUser(
        String githubLogin,
        String displayName,
        String email,
        boolean isAllowedEditor,
        boolean isAdmin
) {
}
