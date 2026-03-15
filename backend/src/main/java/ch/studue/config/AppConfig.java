package ch.studue.config;

import java.io.IOException;
import java.io.InputStream;
import java.nio.file.Path;
import java.util.Properties;

public record AppConfig(
        int port,
        String appBaseUrl,
        String frontendBaseUrl,
        String defaultClassName,
        Path dataDirectory,
        boolean authBypassEnabled,
        String authBypassGithubLogin,
        String authBypassDisplayName,
        String authBypassEmail,
        String githubClientId,
        String githubClientSecret,
        String githubOauthBaseUrl,
        String githubApiBaseUrl,
        String sessionSecret,
        long sessionTtlSeconds
) {
    public static AppConfig load() {
        Properties fileProperties = loadFileProperties();

        return new AppConfig(
                Integer.parseInt(value(fileProperties, "PORT", "8080")),
                value(fileProperties, "APP_BASE_URL", "http://localhost:8080"),
                value(fileProperties, "FRONTEND_BASE_URL", "http://localhost:5173"),
                value(fileProperties, "DEFAULT_CLASS_NAME", "it25ta_win"),
                Path.of(value(fileProperties, "DATA_DIRECTORY", "../data")),
                Boolean.parseBoolean(value(fileProperties, "AUTH_BYPASS_ENABLED", "false")),
                value(fileProperties, "AUTH_BYPASS_GITHUB_LOGIN", "testing-admin"),
                value(fileProperties, "AUTH_BYPASS_DISPLAY_NAME", "Testing Admin"),
                value(fileProperties, "AUTH_BYPASS_EMAIL", "testing-admin@example.invalid"),
                value(fileProperties, "GITHUB_CLIENT_ID", "replace-me"),
                value(fileProperties, "GITHUB_CLIENT_SECRET", "replace-me"),
                value(fileProperties, "GITHUB_OAUTH_BASE_URL", "https://github.zhaw.ch/login/oauth"),
                value(fileProperties, "GITHUB_API_BASE_URL", "https://github.zhaw.ch/api/v3"),
                value(fileProperties, "SESSION_SECRET", "change-me"),
                Long.parseLong(value(fileProperties, "SESSION_TTL_SECONDS", "100000000"))
        );
    }

    private static Properties loadFileProperties() {
        Properties properties = new Properties();
        Path configPath = Path.of("config", "application.properties");

        if (!java.nio.file.Files.exists(configPath)) {
            return properties;
        }

        try (InputStream inputStream = java.nio.file.Files.newInputStream(configPath)) {
            properties.load(inputStream);
            return properties;
        } catch (IOException exception) {
            throw new IllegalStateException("Could not read config file: " + configPath, exception);
        }
    }

    private static String value(Properties properties, String key, String fallback) {
        String value = System.getenv(key);
        if (value != null && !value.isBlank()) {
            return value;
        }

        String propertyValue = properties.getProperty(key);
        return propertyValue == null || propertyValue.isBlank() ? fallback : propertyValue;
    }
}
