package ch.studue.json;

import java.math.BigDecimal;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

public final class SimpleJson {
    private SimpleJson() {
    }

    public static Object parseObject(String json) {
        return new Parser(json).parseValue();
    }

    public static String stringify(Object value) {
        if (value == null) {
            return "null";
        }

        if (value instanceof String string) {
            return '"' + escape(string) + '"';
        }

        if (value instanceof Number || value instanceof Boolean) {
            return String.valueOf(value);
        }

        if (value instanceof Map<?, ?> map) {
            return map.entrySet().stream()
                    .map(entry -> stringify(String.valueOf(entry.getKey())) + ':' + stringify(entry.getValue()))
                    .collect(Collectors.joining(",", "{", "}"));
        }

        if (value instanceof Iterable<?> iterable) {
            StringBuilder builder = new StringBuilder("[");
            Iterator<?> iterator = iterable.iterator();
            while (iterator.hasNext()) {
                builder.append(stringify(iterator.next()));
                if (iterator.hasNext()) {
                    builder.append(',');
                }
            }
            return builder.append(']').toString();
        }

        throw new IllegalArgumentException("Unsupported JSON value: " + value.getClass());
    }

    private static String escape(String input) {
        return input
                .replace("\\", "\\\\")
                .replace("\"", "\\\"")
                .replace("\n", "\\n")
                .replace("\r", "\\r")
                .replace("\t", "\\t");
    }

    private static final class Parser {
        private final String json;
        private int index;

        private Parser(String json) {
            this.json = json;
        }

        private Object parseValue() {
            skipWhitespace();
            char current = json.charAt(index);
            return switch (current) {
                case '{' -> parseObject();
                case '[' -> parseArray();
                case '"' -> parseString();
                case 't' -> parseLiteral("true", Boolean.TRUE);
                case 'f' -> parseLiteral("false", Boolean.FALSE);
                case 'n' -> parseLiteral("null", null);
                default -> {
                    if (current == '-' || Character.isDigit(current)) {
                        yield parseNumber();
                    }
                    throw new IllegalArgumentException("Unexpected JSON token at position " + index);
                }
            };
        }

        private Map<String, Object> parseObject() {
            java.util.Map<String, Object> map = new java.util.LinkedHashMap<>();
            index++;
            skipWhitespace();

            if (json.charAt(index) == '}') {
                index++;
                return map;
            }

            while (true) {
                String key = parseString();
                skipWhitespace();
                expect(':');
                Object value = parseValue();
                map.put(key, value);
                skipWhitespace();

                if (json.charAt(index) == '}') {
                    index++;
                    return map;
                }

                expect(',');
            }
        }

        private List<Object> parseArray() {
            java.util.List<Object> list = new java.util.ArrayList<>();
            index++;
            skipWhitespace();

            if (json.charAt(index) == ']') {
                index++;
                return list;
            }

            while (true) {
                list.add(parseValue());
                skipWhitespace();

                if (json.charAt(index) == ']') {
                    index++;
                    return list;
                }

                expect(',');
            }
        }

        private String parseString() {
            expect('"');
            StringBuilder builder = new StringBuilder();

            while (index < json.length()) {
                char current = json.charAt(index++);
                if (current == '"') {
                    return builder.toString();
                }

                if (current == '\\') {
                    char escaped = json.charAt(index++);
                    builder.append(switch (escaped) {
                        case '"' -> '"';
                        case '\\' -> '\\';
                        case '/' -> '/';
                        case 'b' -> '\b';
                        case 'f' -> '\f';
                        case 'n' -> '\n';
                        case 'r' -> '\r';
                        case 't' -> '\t';
                        default -> escaped;
                    });
                } else {
                    builder.append(current);
                }
            }

            throw new IllegalArgumentException("Unterminated JSON string");
        }

        private Object parseLiteral(String literal, Object value) {
            if (!json.startsWith(literal, index)) {
                throw new IllegalArgumentException("Invalid JSON literal at position " + index);
            }

            index += literal.length();
            return value;
        }

        private Number parseNumber() {
            int start = index;

            if (json.charAt(index) == '-') {
                index++;
            }

            while (index < json.length() && Character.isDigit(json.charAt(index))) {
                index++;
            }

            if (index < json.length() && json.charAt(index) == '.') {
                index++;
                while (index < json.length() && Character.isDigit(json.charAt(index))) {
                    index++;
                }
            }

            if (index < json.length() && (json.charAt(index) == 'e' || json.charAt(index) == 'E')) {
                index++;
                if (json.charAt(index) == '+' || json.charAt(index) == '-') {
                    index++;
                }
                while (index < json.length() && Character.isDigit(json.charAt(index))) {
                    index++;
                }
            }

            String value = json.substring(start, index);
            try {
                return new BigDecimal(value);
            } catch (NumberFormatException exception) {
                throw new IllegalArgumentException("Invalid JSON number at position " + start, exception);
            }
        }

        private void expect(char character) {
            skipWhitespace();
            if (json.charAt(index) != character) {
                throw new IllegalArgumentException("Expected '" + character + "' at position " + index);
            }
            index++;
            skipWhitespace();
        }

        private void skipWhitespace() {
            while (index < json.length() && Character.isWhitespace(json.charAt(index))) {
                index++;
            }
        }
    }
}
