package ch.studue;

import ch.studue.audit.AuditLogStore;
import ch.studue.assignment.AssignmentHandler;
import ch.studue.auth.AccessControlStore;
import ch.studue.auth.AdminHandler;
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
import java.nio.file.Path;
import java.util.concurrent.Executors;

public final class App {
    private App() {
    }

    public static void main(String[] args) throws IOException {
        AppConfig config = AppConfig.load();
        AssignmentRepository repository = new AssignmentRepository(config.dataDirectory(), config.defaultClassName());
        AuditLogStore auditLogStore = new AuditLogStore(config.dataDirectory().resolve("logs"));
        AssignmentService assignmentService = new AssignmentService(repository, config.defaultClassName(), auditLogStore);
        AccessControlStore accessControlStore = new AccessControlStore(Path.of("config", "access-control.json"));
        AuthorizationService authorizationService = new AuthorizationService(accessControlStore);
        SessionService sessionService = new SessionService(config.sessionSecret(), config.sessionTtlSeconds());
        OAuthStateService oAuthStateService = new OAuthStateService(600);
        GitHubOAuthService gitHubOAuthService = new GitHubOAuthService(config);

        HttpServer server = HttpServer.create(new InetSocketAddress(config.port()), 0);
        server.createContext("/api/assignments", new AssignmentHandler(assignmentService, sessionService, authorizationService));
        server.createContext("/api/auth", new AuthHandler(config, sessionService, authorizationService, oAuthStateService, gitHubOAuthService));
        server.createContext("/api/admin", new AdminHandler(sessionService, authorizationService, auditLogStore));
        server.createContext("/health", new HealthHandler());
        server.setExecutor(Executors.newFixedThreadPool(8));
        server.start();

        System.out.println("Studue backend listening on http://localhost:" + config.port());
    }
}
