package ch.studue;

import ch.studue.assignment.AssignmentHandler;
import ch.studue.assignment.AssignmentService;
import ch.studue.auth.AuthHandler;
import ch.studue.auth.AuthorizationService;
import ch.studue.auth.GitHubOAuthService;
import ch.studue.auth.OAuthStateService;
import ch.studue.auth.SessionService;
import ch.studue.config.AppConfig;
import ch.studue.http.HealthHandler;
import ch.studue.storage.AssignmentRepository;
import com.sun.net.httpserver.HttpServer;
import java.io.IOException;
import java.net.InetSocketAddress;
import java.util.concurrent.Executors;

public final class App {
    private App() {
    }

    public static void main(String[] args) throws IOException {
        AppConfig config = AppConfig.load();
        AssignmentRepository repository = new AssignmentRepository(config.dataDirectory(), config.defaultClassName());
        AssignmentService assignmentService = new AssignmentService(repository, config.defaultClassName());
        AuthorizationService authorizationService = new AuthorizationService();
        SessionService sessionService = new SessionService(config.sessionSecret(), config.sessionTtlSeconds());
        OAuthStateService oAuthStateService = new OAuthStateService(600);
        GitHubOAuthService gitHubOAuthService = new GitHubOAuthService(config);

        HttpServer server = HttpServer.create(new InetSocketAddress(config.port()), 0);
        server.createContext("/api/assignments", new AssignmentHandler(assignmentService, sessionService, authorizationService));
        server.createContext("/api/auth", new AuthHandler(config, sessionService, authorizationService, oAuthStateService, gitHubOAuthService));
        server.createContext("/health", new HealthHandler());
        server.setExecutor(Executors.newFixedThreadPool(8));
        server.start();

        System.out.println("Studue backend listening on http://localhost:" + config.port());
    }
}
