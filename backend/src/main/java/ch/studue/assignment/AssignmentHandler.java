package ch.studue.assignment;

import ch.studue.auth.AuthorizationService;
import ch.studue.auth.Session;
import ch.studue.auth.SessionService;
import ch.studue.config.AppConfig;
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
    private final AppConfig config;

    public AssignmentHandler(
            AssignmentService assignmentService,
            SessionService sessionService,
            AuthorizationService authorizationService,
            AppConfig config
    ) {
        this.assignmentService = assignmentService;
        this.sessionService = sessionService;
        this.authorizationService = authorizationService;
        this.config = config;
    }

    @Override
    public void handle(HttpExchange exchange) throws IOException {
        try {
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

            Optional<Session> session = HttpExchangeHelper.findSession(exchange, sessionService, config);
            if (session.isEmpty() || !authorizationService.isAllowedEditor(session.get().user().githubLogin())) {
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
                boolean deleted = assignmentService.delete(id, actor);
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
        } catch (AssignmentValidationException exception) {
            HttpExchangeHelper.badRequest(exchange, exception.code(), exception.getMessage(), exception.details());
        } catch (IllegalArgumentException exception) {
            HttpExchangeHelper.badRequest(
                    exchange,
                    "invalid_json",
                    "The request body must contain valid JSON.",
                    Map.of("reason", exception.getMessage())
            );
        }
    }

    private AssignmentDraft readDraft(HttpExchange exchange) throws IOException {
        @SuppressWarnings("unchecked")
        Map<String, Object> body = (Map<String, Object>) SimpleJson.parseObject(HttpExchangeHelper.readBody(exchange));

        if (!(body.get("module") instanceof String)) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The module field is required.",
                    Map.of("field", "module")
            );
        }

        if (!(body.get("title") instanceof String)) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The title field is required.",
                    Map.of("field", "title")
            );
        }

        if (!(body.get("dueDate") instanceof String)) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The dueDate field is required.",
                    Map.of("field", "dueDate")
            );
        }

        if (!(body.get("mandatory") instanceof Boolean)) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The mandatory field must be a boolean.",
                    Map.of("field", "mandatory")
            );
        }

        return new AssignmentDraft(
                stringValue(body.get("module")).trim(),
                stringValue(body.get("title")).trim(),
                stringValue(body.get("dueDate")).trim(),
                stringValue(body.getOrDefault("dueTime", "")).trim(),
                stringValue(body.getOrDefault("note", "")).trim(),
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
        result.put("dueTime", assignment.dueTime());
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
