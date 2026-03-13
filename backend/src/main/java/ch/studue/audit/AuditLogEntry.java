package ch.studue.audit;

import java.util.Map;

public record AuditLogEntry(
        String timestamp,
        String action,
        String actorLogin,
        String actorDisplayName,
        String assignmentId,
        String title,
        String dueDate,
        String dueTime,
        Map<String, Map<String, String>> changes,
        Map<String, Object> snapshot
) {
}
