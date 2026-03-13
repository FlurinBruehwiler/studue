package ch.studue.auth;

import java.io.IOException;

public final class AuthorizationService {
    private final AccessControlStore accessControlStore;

    public AuthorizationService(AccessControlStore accessControlStore) {
        this.accessControlStore = accessControlStore;
    }

    public boolean isAllowedEditor(String githubLogin) throws IOException {
        if (githubLogin == null || githubLogin.isBlank()) {
            return false;
        }

        AccessControl accessControl = accessControlStore.read();
        return accessControl.admins().contains(githubLogin) || accessControl.editors().contains(githubLogin);
    }

    public boolean isAdmin(String githubLogin) throws IOException {
        if (githubLogin == null || githubLogin.isBlank()) {
            return false;
        }

        return accessControlStore.read().admins().contains(githubLogin);
    }

    public AccessControl getAccessControl() throws IOException {
        return accessControlStore.read();
    }

    public void addEditor(String githubLogin) throws IOException {
        accessControlStore.addEditor(githubLogin);
    }

    public void removeEditor(String githubLogin) throws IOException {
        accessControlStore.removeEditor(githubLogin);
    }

    public void addAdmin(String githubLogin) throws IOException {
        accessControlStore.addAdmin(githubLogin);
    }
}
