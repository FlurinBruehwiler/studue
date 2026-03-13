package ch.studue.assignment;

public record AssignmentDraft(
        String module,
        String title,
        String dueDate,
        String note,
        boolean mandatory
) {
}
