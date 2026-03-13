package ch.studue.audit;

public record AuditLogEntry(
        String timestamp,
        String action,
        String actorLogin,
        String actorDisplayName,
        String assignmentId,
        String title,
        String dueDate,
        String dueTime
) {
}
