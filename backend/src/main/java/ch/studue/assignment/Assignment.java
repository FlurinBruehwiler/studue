package ch.studue.assignment;

public record Assignment(
        String id,
        String className,
        String module,
        String title,
        String dueDate,
        String dueTime,
        String note,
        boolean mandatory,
        AssignmentUser createdBy,
        AssignmentUser updatedBy,
        String createdAt,
        String updatedAt
) {
}
