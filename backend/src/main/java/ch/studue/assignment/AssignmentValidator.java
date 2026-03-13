package ch.studue.assignment;

import java.time.LocalDate;
import java.time.LocalTime;
import java.time.format.DateTimeParseException;
import java.util.Map;

public final class AssignmentValidator {
    public void validate(AssignmentDraft draft) {
        requireLength("module", draft.module(), 1, 120);
        requireLength("title", draft.title(), 1, 200);
        requireDate(draft.dueDate());
        requireOptionalTime(draft.dueTime());

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

    private void requireDate(String dueDate) {
        try {
            LocalDate.parse(dueDate);
        } catch (DateTimeParseException exception) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The dueDate field must use YYYY-MM-DD.",
                    Map.of("field", "dueDate")
            );
        }
    }

    private void requireOptionalTime(String dueTime) {
        if (dueTime == null || dueTime.isBlank()) {
            return;
        }

        try {
            LocalTime.parse(dueTime);
        } catch (DateTimeParseException exception) {
            throw new AssignmentValidationException(
                    "validation_error",
                    "The dueTime field must use HH:MM.",
                    Map.of("field", "dueTime")
            );
        }
    }
}
