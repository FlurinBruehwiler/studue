package ch.studue.assignment;

import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.LocalTime;
import java.time.format.DateTimeParseException;
import java.util.Map;

public final class AssignmentValidator {
    public void validate(AssignmentDraft draft) {
        requireLength("module", draft.module(), 1, 120);
        requireLength("title", draft.title(), 1, 200);
        LocalDate dueDate = requireDate(draft.dueDate());
        LocalTime dueTime = requireOptionalTime(draft.dueTime());
        requireNotPast(dueDate, dueTime);

        if (draft.note() != null && draft.note().length() > 5000) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The note field must be 5000 characters or fewer.",
                    Map.of("field", "note")
            );
        }
    }

    private void requireLength(String field, String value, int min, int max) {
        String trimmed = value == null ? "" : value.trim();
        if (trimmed.length() < min || trimmed.length() > max) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The " + field + " field must be between " + min + " and " + max + " characters.",
                    Map.of("field", field)
            );
        }
    }

    private LocalDate requireDate(String dueDate) {
        try {
            return LocalDate.parse(dueDate);
        } catch (DateTimeParseException exception) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The dueDate field must use YYYY-MM-DD.",
                    Map.of("field", "dueDate")
            );
        }
    }

    private LocalTime requireOptionalTime(String dueTime) {
        if (dueTime == null || dueTime.isBlank()) {
            return null;
        }

        try {
            return LocalTime.parse(dueTime);
        } catch (DateTimeParseException exception) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The dueTime field must use HH:MM.",
                    Map.of("field", "dueTime")
            );
        }
    }

    private void requireNotPast(LocalDate dueDate, LocalTime dueTime) {
        LocalDate today = LocalDate.now();

        if (dueDate.isBefore(today)) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "Assignments cannot be created or updated in the past.",
                    Map.of("field", "dueDate")
            );
        }

        if (dueTime != null && dueDate.isEqual(today)) {
            LocalDateTime dueDateTime = LocalDateTime.of(dueDate, dueTime);
            if (dueDateTime.isBefore(LocalDateTime.now())) {
                throw new AssignmentValidationException(
                        "validation_error",
                        "Assignments cannot be created or updated in the past.",
                        Map.of("field", "dueTime")
                );
            }
        }
    }
}
