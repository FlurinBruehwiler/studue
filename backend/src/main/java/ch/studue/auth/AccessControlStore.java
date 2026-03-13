package ch.studue.auth;

import ch.studue.json.SimpleJson;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class AccessControlStore {
    private final Path filePath;

    public AccessControlStore(Path filePath) throws IOException {
        this.filePath = filePath;
        Files.createDirectories(filePath.getParent());

        if (!Files.exists(filePath)) {
            write(new AccessControl(List.of("bruehflu"), List.of("bruehflu")));
        }
    }

    public synchronized AccessControl read() throws IOException {
        @SuppressWarnings("unchecked")
        Map<String, Object> raw = (Map<String, Object>) SimpleJson.parseObject(Files.readString(filePath, StandardCharsets.UTF_8));
        return new AccessControl(stringList(raw.get("admins")), stringList(raw.get("editors")));
    }

    public synchronized void addEditor(String githubLogin) throws IOException {
        AccessControl current = read();
        List<String> editors = new ArrayList<>(current.editors());
        if (!editors.contains(githubLogin)) {
            editors.add(githubLogin);
            write(new AccessControl(current.admins(), editors));
        }
    }

    public synchronized void removeEditor(String githubLogin) throws IOException {
        AccessControl current = read();
        List<String> editors = new ArrayList<>(current.editors());
        editors.removeIf(login -> login.equals(githubLogin));
        write(new AccessControl(current.admins(), editors));
    }

    public synchronized void addAdmin(String githubLogin) throws IOException {
        AccessControl current = read();
        List<String> admins = new ArrayList<>(current.admins());
        if (!admins.contains(githubLogin)) {
            admins.add(githubLogin);
            write(new AccessControl(admins, current.editors()));
        }
    }

    private synchronized void write(AccessControl accessControl) throws IOException {
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("admins", normalize(accessControl.admins()));
        body.put("editors", normalize(accessControl.editors()).stream().filter(login -> !accessControl.admins().contains(login)).toList());

        Path tempPath = filePath.resolveSibling(filePath.getFileName() + ".tmp");
        Files.writeString(tempPath, SimpleJson.stringify(body), StandardCharsets.UTF_8);
        Files.move(tempPath, filePath, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE);
    }

    private List<String> stringList(Object value) {
        if (!(value instanceof List<?> items)) {
            return List.of();
        }

        return normalize(items.stream().map(String::valueOf).toList());
    }

    private List<String> normalize(List<String> values) {
        return values.stream().distinct().sorted().toList();
    }
}
