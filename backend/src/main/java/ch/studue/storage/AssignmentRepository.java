package ch.studue.storage;

import ch.studue.assignment.Assignment;
import ch.studue.assignment.AssignmentUser;
import ch.studue.json.SimpleJson;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Stream;

public final class AssignmentRepository {
    private final Path classDirectory;

    public AssignmentRepository(Path dataDirectory, String className) throws IOException {
        this.classDirectory = dataDirectory.resolve(className);
        Files.createDirectories(classDirectory);
    }

    public List<Assignment> list(Map<String, String> filters) throws IOException {
        try (Stream<Path> stream = Files.list(classDirectory)) {
            return stream
                    .filter(path -> path.getFileName().toString().endsWith(".json"))
                    .filter(path -> matchesDatePrefix(path, filters.get("from"), filters.get("to")))
                    .map(this::readQuietly)
                    .filter(item -> item != null)
                    .filter(item -> matchesFilters(item, filters))
                    .sorted(Comparator.comparing(Assignment::dueDate)
                            .thenComparing(assignment -> assignment.dueTime() == null ? "" : assignment.dueTime())
                            .thenComparing(Assignment::title))
                    .toList();
        }
    }

    public Assignment getById(String id) throws IOException {
        Path path = classDirectory.resolve(id + ".json");
        if (!Files.exists(path)) {
            return null;
        }

        return read(path);
    }

    public void save(Assignment assignment) throws IOException {
        Path path = classDirectory.resolve(assignment.id() + ".json");
        Path tempPath = classDirectory.resolve(assignment.id() + ".tmp");
        Files.writeString(tempPath, SimpleJson.stringify(toMap(assignment)), StandardCharsets.UTF_8);
        Files.move(tempPath, path, StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE);
    }

    public boolean delete(String id) throws IOException {
        Path path = classDirectory.resolve(id + ".json");
        return Files.deleteIfExists(path);
    }

    private Assignment read(Path path) throws IOException {
        @SuppressWarnings("unchecked")
        Map<String, Object> map = (Map<String, Object>) SimpleJson.parseObject(Files.readString(path, StandardCharsets.UTF_8));
        return fromMap(map);
    }

    private Assignment readQuietly(Path path) {
        try {
            return read(path);
        } catch (IOException exception) {
            return null;
        }
    }

    private boolean matchesDatePrefix(Path path, String from, String to) {
        String fileName = path.getFileName().toString();
        if (fileName.length() < 10) {
            return true;
        }

        String datePrefix = fileName.substring(0, 10);
        if (from != null && !from.isBlank() && datePrefix.compareTo(from) < 0) {
            return false;
        }

        return to == null || to.isBlank() || datePrefix.compareTo(to) <= 0;
    }

    private boolean matchesFilters(Assignment assignment, Map<String, String> filters) {
        String module = filters.get("module");
        String mandatory = filters.get("mandatory");
        String from = filters.get("from");
        String to = filters.get("to");

        if (module != null && !module.isBlank() && !assignment.module().equals(module)) {
            return false;
        }

        if (mandatory != null && !mandatory.isBlank() && assignment.mandatory() != Boolean.parseBoolean(mandatory)) {
            return false;
        }

        if (from != null && !from.isBlank() && assignment.dueDate().compareTo(from) < 0) {
            return false;
        }

        return to == null || to.isBlank() || assignment.dueDate().compareTo(to) <= 0;
    }

    private Assignment fromMap(Map<String, Object> map) {
        @SuppressWarnings("unchecked")
        Map<String, Object> createdBy = (Map<String, Object>) map.get("createdBy");
        @SuppressWarnings("unchecked")
        Map<String, Object> updatedBy = (Map<String, Object>) map.get("updatedBy");

        return new Assignment(
                string(map.get("id")),
                string(map.get("className")),
                string(map.get("module")),
                string(map.get("title")),
                string(map.get("dueDate")),
                string(map.get("dueTime")),
                string(map.get("note")),
                Boolean.TRUE.equals(map.get("mandatory")),
                new AssignmentUser(string(createdBy.get("githubLogin")), string(createdBy.get("displayName")), string(createdBy.get("email"))),
                new AssignmentUser(string(updatedBy.get("githubLogin")), string(updatedBy.get("displayName")), string(updatedBy.get("email"))),
                string(map.get("createdAt")),
                string(map.get("updatedAt"))
        );
    }

    private Map<String, Object> toMap(Assignment assignment) {
        Map<String, Object> result = new LinkedHashMap<>();
        result.put("id", assignment.id());
        result.put("className", assignment.className());
        result.put("module", assignment.module());
        result.put("title", assignment.title());
        result.put("dueDate", assignment.dueDate());
        result.put("dueTime", assignment.dueTime());
        result.put("note", assignment.note());
        result.put("mandatory", assignment.mandatory());
        result.put("createdBy", Map.of(
                "githubLogin", assignment.createdBy().githubLogin(),
                "displayName", assignment.createdBy().displayName(),
                "email", assignment.createdBy().email()
        ));
        result.put("updatedBy", Map.of(
                "githubLogin", assignment.updatedBy().githubLogin(),
                "displayName", assignment.updatedBy().displayName(),
                "email", assignment.updatedBy().email()
        ));
        result.put("createdAt", assignment.createdAt());
        result.put("updatedAt", assignment.updatedAt());
        return result;
    }

    private String string(Object value) {
        return value == null ? "" : String.valueOf(value);
    }
}
