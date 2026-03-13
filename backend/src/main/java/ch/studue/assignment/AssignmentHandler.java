package ch.studue.assignment;

import ch.studue.auth.AuthorizationService;
import ch.studue.auth.Session;
import ch.studue.auth.SessionService;
import ch.studue.http.HttpExchangeHelper;
import ch.studue.json.SimpleJson;
import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpHandler;
import java.io.IOException;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Optional;

public final class AssignmentHandler implements HttpHandler {
    private final AssignmentService assignmentService;
    private final SessionService sessionService;
    private final AuthorizationService authorizationService;

    public AssignmentHandler(
            AssignmentService assignmentService,
            SessionService sessionService,
            AuthorizationService authorizationService
    ) {
        this.assignmentService = assignmentService;
        this.sessionService = sessionService;
        this.authorizationService = authorizationService;
    }

    @Override
    public void handle(HttpExchange exchange) throws IOException {
        String method = exchange.getRequestMethod();
        String path = exchange.getRequestURI().getPath();
        String basePath = "/api/assignments";
        String id = path.length() > basePath.length() + 1 ? path.substring(basePath.length() + 1) : null;

        if (method.equalsIgnoreCase("GET") && (id == null || id.isBlank())) {
            List<Assignment> items = assignmentService.list(HttpExchangeHelper.queryParams(exchange));
            HttpExchangeHelper.sendJson(exchange, 200, Map.of("items", items.stream().map(this::toMap).toList()));
            return;
        }

        if (method.equalsIgnoreCase("GET") && id != null) {
            Assignment assignment = assignmentService.getById(id);
            if (assignment == null) {
                HttpExchangeHelper.notFound(exchange, "Assignment not found.");
                return;
            }

            HttpExchangeHelper.sendJson(exchange, 200, Map.of("item", toMap(assignment)));
            return;
        }

        Optional<Session> session = HttpExchangeHelper.findSession(exchange, sessionService);
        if (session.isEmpty() || !authorizationService.isAllowedEditor(session.get().user().email())) {
            HttpExchangeHelper.sendJson(exchange, 403, Map.of(
                    "error", Map.of(
                            "code", "forbidden",
                            "message", "You must be an allowed editor to modify assignments."
                    )
            ));
            return;
        }

        AssignmentUser actor = new AssignmentUser(
                session.get().user().githubLogin(),
                session.get().user().displayName(),
                session.get().user().email()
        );

        if (method.equalsIgnoreCase("POST") && (id == null || id.isBlank())) {
            Assignment created = assignmentService.create(readDraft(exchange), actor);
            HttpExchangeHelper.sendJson(exchange, 201, Map.of("item", toMap(created)));
            return;
        }

        if (method.equalsIgnoreCase("PUT") && id != null) {
            Assignment updated = assignmentService.update(id, readDraft(exchange), actor);
            if (updated == null) {
                HttpExchangeHelper.notFound(exchange, "Assignment not found.");
                return;
            }

            HttpExchangeHelper.sendJson(exchange, 200, Map.of("item", toMap(updated)));
            return;
        }

        if (method.equalsIgnoreCase("DELETE") && id != null) {
            boolean deleted = assignmentService.delete(id);
            if (!deleted) {
                HttpExchangeHelper.notFound(exchange, "Assignment not found.");
                return;
            }

            HttpExchangeHelper.sendJson(exchange, 200, Map.of("ok", true));
            return;
        }

        HttpExchangeHelper.sendJson(exchange, 405, Map.of(
                "error", Map.of(
                        "code", "method_not_allowed",
                        "message", "Method not allowed."
                )
        ));
    }

    private AssignmentDraft readDraft(HttpExchange exchange) throws IOException {
        @SuppressWarnings("unchecked")
        Map<String, Object> body = (Map<String, Object>) SimpleJson.parseObject(HttpExchangeHelper.readBody(exchange));

        return new AssignmentDraft(
                stringValue(body.get("module")),
                stringValue(body.get("title")),
                stringValue(body.get("dueDate")),
                stringValue(body.getOrDefault("note", "")),
                Boolean.TRUE.equals(body.get("mandatory"))
        );
    }

    private Map<String, Object> toMap(Assignment assignment) {
        Map<String, Object> result = new LinkedHashMap<>();
        result.put("id", assignment.id());
        result.put("className", assignment.className());
        result.put("module", assignment.module());
        result.put("title", assignment.title());
        result.put("dueDate", assignment.dueDate());
        result.put("note", assignment.note());
        result.put("mandatory", assignment.mandatory());
        result.put("createdBy", Map.of(
                "githubLogin", assignment.createdBy().githubLogin(),
                "displayName", assignment.createdBy().displayName(),
                "email", assignment.createdBy().email()
        ));
        result.put("updatedBy", Map.of(
                "githubLogin", assignment.updatedBy().githubLogin(),
                "displayName", assignment.updatedBy().displayName(),
                "email", assignment.updatedBy().email()
        ));
        result.put("createdAt", assignment.createdAt());
        result.put("updatedAt", assignment.updatedAt());
        return result;
    }

    private String stringValue(Object value) {
        return value == null ? "" : String.valueOf(value);
    }
}
