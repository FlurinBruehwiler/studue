package ch.studue.assignment;

import ch.studue.audit.AuditLogStore;
import ch.studue.storage.AssignmentRepository;
import java.io.IOException;
import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

public final class AssignmentService {
    private final AssignmentRepository repository;
    private final String defaultClassName;
    private final AssignmentValidator validator;
    private final AuditLogStore auditLogStore;

    public AssignmentService(AssignmentRepository repository, String defaultClassName, AuditLogStore auditLogStore) {
        this.repository = repository;
        this.defaultClassName = defaultClassName;
        this.validator = new AssignmentValidator();
        this.auditLogStore = auditLogStore;
    }

    public List<Assignment> list(Map<String, String> filters) throws IOException {
        return repository.list(filters);
    }

    public Assignment getById(String id) throws IOException {
        return repository.getById(id);
    }

    public Assignment create(AssignmentDraft draft, AssignmentUser actor) throws IOException {
        validator.validate(draft);
        String id = draft.dueDate() + "--" + UUID.randomUUID().toString().substring(0, 8);
        String now = Instant.now().toString();

        Assignment assignment = new Assignment(
                id,
                defaultClassName,
                draft.module(),
                draft.title(),
                draft.dueDate(),
                draft.dueTime(),
                draft.note(),
                draft.mandatory(),
                actor,
                actor,
                now,
                now
        );

        repository.save(assignment);
        auditLogStore.append("add", assignment, actor, Map.of(), Map.of());
        return assignment;
    }

    public Assignment update(String id, AssignmentDraft draft, AssignmentUser actor) throws IOException {
        validator.validate(draft);
        Assignment existing = repository.getById(id);
        if (existing == null) {
            return null;
        }

        Assignment updated = new Assignment(
                existing.id(),
                existing.className(),
                draft.module(),
                draft.title(),
                draft.dueDate(),
                draft.dueTime(),
                draft.note(),
                draft.mandatory(),
                existing.createdBy(),
                actor,
                existing.createdAt(),
                Instant.now().toString()
        );

        repository.save(updated);
        auditLogStore.append("edit", updated, actor, diff(existing, updated), Map.of());
        return updated;
    }

    public boolean delete(String id, AssignmentUser actor) throws IOException {
        Assignment existing = repository.getById(id);
        if (existing == null) {
            return false;
        }

        boolean deleted = repository.delete(id);
        if (deleted) {
            auditLogStore.append("delete", existing, actor, Map.of(), snapshot(existing));
        }
        return deleted;
    }

    public Assignment restoreDeletedAssignment(Map<String, Object> snapshot) throws IOException {
        Assignment restored = new Assignment(
                stringValue(snapshot.get("id")),
                stringValue(snapshot.get("className")),
                stringValue(snapshot.get("module")),
                stringValue(snapshot.get("title")),
                stringValue(snapshot.get("dueDate")),
                stringValue(snapshot.get("dueTime")),
                stringValue(snapshot.get("note")),
                Boolean.TRUE.equals(snapshot.get("mandatory")),
                toUser(snapshot.get("createdBy")),
                toUser(snapshot.get("updatedBy")),
                stringValue(snapshot.get("createdAt")),
                stringValue(snapshot.get("updatedAt"))
        );

        repository.save(restored);
        return restored;
    }

    private Map<String, Map<String, String>> diff(Assignment before, Assignment after) {
        Map<String, Map<String, String>> changes = new LinkedHashMap<>();
        addChange(changes, "module", before.module(), after.module());
        addChange(changes, "title", before.title(), after.title());
        addChange(changes, "dueDate", before.dueDate(), after.dueDate());
        addChange(changes, "dueTime", before.dueTime(), after.dueTime());
        addChange(changes, "note", before.note(), after.note());
        addChange(changes, "mandatory", String.valueOf(before.mandatory()), String.valueOf(after.mandatory()));
        return changes;
    }

    private void addChange(Map<String, Map<String, String>> changes, String field, String before, String after) {
        String left = before == null ? "" : before;
        String right = after == null ? "" : after;
        if (!left.equals(right)) {
            changes.put(field, Map.of("before", left, "after", right));
        }
    }

    private Map<String, Object> snapshot(Assignment assignment) {
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

    @SuppressWarnings("unchecked")
    private AssignmentUser toUser(Object value) {
        if (!(value instanceof Map<?, ?> raw)) {
            return new AssignmentUser("", "", "");
        }

        return new AssignmentUser(
                stringValue(raw.get("githubLogin")),
                stringValue(raw.get("displayName")),
                stringValue(raw.get("email"))
        );
    }

    private String stringValue(Object value) {
        return value == null ? "" : String.valueOf(value);
    }
}
