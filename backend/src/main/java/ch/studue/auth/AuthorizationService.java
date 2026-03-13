package ch.studue.auth;

import java.util.Set;

public final class AuthorizationService {
    private static final Set<String> ALLOWED_EMAILS = Set.of(
            "jane.doe@students.zhaw.ch",
            "max.muster@students.zhaw.ch",
            "chris.student@students.zhaw.ch"
    );

    public boolean isAllowedEditor(String email) {
        return email != null && ALLOWED_EMAILS.contains(email.toLowerCase());
    }
}
