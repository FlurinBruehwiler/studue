package ch.studue.auth;

import ch.studue.config.AppConfig;
import ch.studue.json.SimpleJson;
import java.io.IOException;
import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.List;
import java.util.Map;

public final class GitHubOAuthService {
    private final AppConfig config;
    private final HttpClient httpClient;

    public GitHubOAuthService(AppConfig config) {
        this.config = config;
        this.httpClient = HttpClient.newBuilder()
                .connectTimeout(Duration.ofSeconds(10))
                .build();
    }

    public String buildAuthorizeUrl(String state) {
        return config.githubOauthBaseUrl()
                + "/authorize?client_id=" + encode(config.githubClientId())
                + "&redirect_uri=" + encode(callbackUrl())
                + "&state=" + encode(state)
                + "&scope=" + encode("read:user user:email");
    }

    public GitHubUserProfile authenticate(String code) throws IOException {
        String accessToken = exchangeCodeForToken(code);
        return fetchUserProfile(accessToken);
    }

    private String exchangeCodeForToken(String code) throws IOException {
        String body = "client_id=" + encode(config.githubClientId())
                + "&client_secret=" + encode(config.githubClientSecret())
                + "&code=" + encode(code)
                + "&redirect_uri=" + encode(callbackUrl());

        HttpRequest request = HttpRequest.newBuilder()
                .uri(URI.create(config.githubOauthBaseUrl() + "/access_token"))
                .header("Accept", "application/json")
                .header("Content-Type", "application/x-www-form-urlencoded")
                .POST(HttpRequest.BodyPublishers.ofString(body))
                .build();

        @SuppressWarnings("unchecked")
        Map<String, Object> response = (Map<String, Object>) executeJson(request);
        Object token = response.get("access_token");
        if (!(token instanceof String accessToken) || accessToken.isBlank()) {
            throw new IOException("GitHub OAuth response did not include an access token.");
        }

        return accessToken;
    }

    private GitHubUserProfile fetchUserProfile(String accessToken) throws IOException {
        @SuppressWarnings("unchecked")
        Map<String, Object> user = (Map<String, Object>) executeJson(apiRequest("/user", accessToken));

        String login = stringValue(user.get("login"));
        String displayName = stringValue(user.get("name"));
        String fallbackEmail = stringValue(user.get("email"));
        String email = fallbackEmail.isBlank() ? fetchPrimaryEmail(accessToken) : fallbackEmail;

        if (login.isBlank()) {
            throw new IOException("GitHub user response did not include a login.");
        }

        return new GitHubUserProfile(
                login,
                displayName.isBlank() ? login : displayName,
                email
        );
    }

    private String fetchPrimaryEmail(String accessToken) throws IOException {
        @SuppressWarnings("unchecked")
        List<Object> emails = (List<Object>) executeJson(apiRequest("/user/emails", accessToken));

        String firstVerified = "";
        for (Object item : emails) {
            if (!(item instanceof Map<?, ?> rawEmail)) {
                continue;
            }

            String email = stringValue(rawEmail.get("email"));
            boolean primary = Boolean.TRUE.equals(rawEmail.get("primary"));
            boolean verified = Boolean.TRUE.equals(rawEmail.get("verified"));

            if (verified && firstVerified.isBlank()) {
                firstVerified = email;
            }

            if (primary && verified && !email.isBlank()) {
                return email;
            }
        }

        return firstVerified;
    }

    private HttpRequest apiRequest(String path, String accessToken) {
        return HttpRequest.newBuilder()
                .uri(URI.create(config.githubApiBaseUrl() + path))
                .header("Accept", "application/json")
                .header("Authorization", "Bearer " + accessToken)
                .header("X-GitHub-Api-Version", "2022-11-28")
                .GET()
                .build();
    }

    private Object executeJson(HttpRequest request) throws IOException {
        try {
            HttpResponse<String> response = httpClient.send(request, HttpResponse.BodyHandlers.ofString(StandardCharsets.UTF_8));
            if (response.statusCode() < 200 || response.statusCode() >= 300) {
                throw new IOException("GitHub request failed with status " + response.statusCode() + '.');
            }
            return SimpleJson.parseObject(response.body());
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new IOException("GitHub request interrupted.", exception);
        }
    }

    private String callbackUrl() {
        return config.appBaseUrl() + "/api/auth/callback";
    }

    private String encode(String value) {
        return URLEncoder.encode(value, StandardCharsets.UTF_8);
    }

    private String stringValue(Object value) {
        return value == null ? "" : String.valueOf(value);
    }
}
