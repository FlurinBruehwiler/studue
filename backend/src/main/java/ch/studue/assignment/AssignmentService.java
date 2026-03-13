package ch.studue.assignment;

import ch.studue.audit.AuditLogStore;
import ch.studue.storage.AssignmentRepository;
import java.io.IOException;
import java.time.Instant;
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
        auditLogStore.append("add", assignment, actor);
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
        auditLogStore.append("edit", updated, actor);
        return updated;
    }

    public boolean delete(String id, AssignmentUser actor) throws IOException {
        Assignment existing = repository.getById(id);
        if (existing == null) {
            return false;
        }

        boolean deleted = repository.delete(id);
        if (deleted) {
            auditLogStore.append("delete", existing, actor);
        }
        return deleted;
    }
}
