package ch.studue.assignment;

import java.util.Map;

public final class AssignmentValidationException extends RuntimeException {
    private final String code;
    private final Map<String, Object> details;

    public AssignmentValidationException(String code, String message, Map<String, Object> details) {
        super(message);
        this.code = code;
        this.details = details;
    }

    public String code() {
        return code;
    }

    public Map<String, Object> details() {
        return details;
    }
}
