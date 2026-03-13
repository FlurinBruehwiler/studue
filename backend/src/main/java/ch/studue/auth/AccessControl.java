package ch.studue.auth;

import java.util.List;

public record AccessControl(
        List<String> admins,
        List<String> editors
) {
}
