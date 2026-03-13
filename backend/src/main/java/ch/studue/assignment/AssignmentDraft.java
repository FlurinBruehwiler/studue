package ch.studue.assignment;

public record AssignmentDraft(
        String module,
        String title,
        String dueDate,
        String dueTime,
        String note,
        boolean mandatory
) {
}
