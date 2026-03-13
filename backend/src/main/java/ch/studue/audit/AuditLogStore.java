package ch.studue.audit;

import ch.studue.assignment.Assignment;
import ch.studue.assignment.AssignmentUser;
import ch.studue.json.SimpleJson;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.time.LocalDate;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Stream;

public final class AuditLogStore {
    private final Path logsDirectory;

    public AuditLogStore(Path logsDirectory) throws IOException {
        this.logsDirectory = logsDirectory;
        Files.createDirectories(logsDirectory);
    }

    public synchronized void append(String action, Assignment assignment, AssignmentUser actor) throws IOException {
        append(action, assignment, actor, Map.of(), Map.of());
    }

    public synchronized void append(
            String action,
            Assignment assignment,
            AssignmentUser actor,
            Map<String, Map<String, String>> changes,
            Map<String, Object> snapshot
    ) throws IOException {
        AuditLogEntry entry = new AuditLogEntry(
                java.time.Instant.now().toString(),
                action,
                actor.githubLogin(),
                actor.displayName(),
                assignment.id(),
                assignment.title(),
                assignment.dueDate(),
                assignment.dueTime(),
                changes,
                snapshot
        );

        String fileName = LocalDate.now() + ".log";
        Path logPath = logsDirectory.resolve(fileName);
        Files.writeString(
                logPath,
                SimpleJson.stringify(toMap(entry)) + System.lineSeparator(),
                StandardCharsets.UTF_8,
                StandardOpenOption.CREATE,
                StandardOpenOption.APPEND
        );
    }

    public synchronized List<AuditLogEntry> readAll() throws IOException {
        try (Stream<Path> stream = Files.list(logsDirectory)) {
            return stream
                    .filter(path -> path.getFileName().toString().endsWith(".log"))
                    .sorted(Comparator.reverseOrder())
                    .flatMap(this::readFile)
                    .sorted(Comparator.comparing(AuditLogEntry::timestamp).reversed())
                    .toList();
        }
    }

    private Stream<AuditLogEntry> readFile(Path path) {
        try {
            return Files.readAllLines(path, StandardCharsets.UTF_8).stream()
                    .filter(line -> !line.isBlank())
                    .map(this::parseLine);
        } catch (IOException exception) {
            return Stream.empty();
        }
    }

    private AuditLogEntry parseLine(String line) {
        @SuppressWarnings("unchecked")
        Map<String, Object> raw = (Map<String, Object>) SimpleJson.parseObject(line);
        return new AuditLogEntry(
                stringValue(raw.get("timestamp")),
                stringValue(raw.get("action")),
                stringValue(raw.get("actorLogin")),
                stringValue(raw.get("actorDisplayName")),
                stringValue(raw.get("assignmentId")),
                stringValue(raw.get("title")),
                stringValue(raw.get("dueDate")),
                stringValue(raw.get("dueTime")),
                changeMap(raw.get("changes")),
                objectMap(raw.get("snapshot"))
        );
    }

    private Map<String, Object> toMap(AuditLogEntry entry) {
        Map<String, Object> map = new LinkedHashMap<>();
        map.put("timestamp", entry.timestamp());
        map.put("action", entry.action());
        map.put("actorLogin", entry.actorLogin());
        map.put("actorDisplayName", entry.actorDisplayName());
        map.put("assignmentId", entry.assignmentId());
        map.put("title", entry.title());
        map.put("dueDate", entry.dueDate());
        map.put("dueTime", entry.dueTime());
        map.put("changes", entry.changes());
        map.put("snapshot", entry.snapshot());
        return map;
    }

    private Map<String, Map<String, String>> changeMap(Object value) {
        if (!(value instanceof Map<?, ?> rawMap)) {
            return Map.of();
        }

        Map<String, Map<String, String>> result = new LinkedHashMap<>();
        rawMap.forEach((key, rawEntry) -> {
            if (rawEntry instanceof Map<?, ?> rawChange) {
                result.put(String.valueOf(key), Map.of(
                        "before", stringValue(rawChange.get("before")),
                        "after", stringValue(rawChange.get("after"))
                ));
            }
        });
        return result;
    }

    private Map<String, Object> objectMap(Object value) {
        if (!(value instanceof Map<?, ?> rawMap)) {
            return Map.of();
        }

        Map<String, Object> result = new LinkedHashMap<>();
        rawMap.forEach((key, rawValue) -> result.put(String.valueOf(key), rawValue));
        return result;
    }

    private String stringValue(Object value) {
        return value == null ? "" : String.valueOf(value);
    }
}
