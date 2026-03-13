package ch.studue.config;

import java.nio.file.Path;

public record AppConfig(
        int port,
        String appBaseUrl,
        String defaultClassName,
        Path dataDirectory,
        String githubClientId,
        String githubClientSecret,
        String githubOauthBaseUrl,
        String githubApiBaseUrl,
        String sessionSecret,
        long sessionTtlSeconds
) {
    public static AppConfig load() {
        return new AppConfig(
                Integer.parseInt(envOrDefault("PORT", "8080")),
                envOrDefault("APP_BASE_URL", "http://localhost:8080"),
                envOrDefault("DEFAULT_CLASS_NAME", "it25ta_win"),
                Path.of(envOrDefault("DATA_DIRECTORY", "../data")),
                envOrDefault("GITHUB_CLIENT_ID", "replace-me"),
                envOrDefault("GITHUB_CLIENT_SECRET", "replace-me"),
                envOrDefault("GITHUB_OAUTH_BASE_URL", "https://github.zhaw.ch/login/oauth"),
                envOrDefault("GITHUB_API_BASE_URL", "https://github.zhaw.ch/api/v3"),
                envOrDefault("SESSION_SECRET", "change-me"),
                Long.parseLong(envOrDefault("SESSION_TTL_SECONDS", "43200"))
        );
    }

    private static String envOrDefault(String key, String fallback) {
        String value = System.getenv(key);
        return value == null || value.isBlank() ? fallback : value;
    }
}
