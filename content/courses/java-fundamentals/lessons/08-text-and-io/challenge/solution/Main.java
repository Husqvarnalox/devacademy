import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

public class Main {
    static List<String> parse(String line) {
        List<String> fields = new ArrayList<>();
        StringBuilder cur = new StringBuilder();
        int i = 0;
        int len = line.length();
        while (true) {
            cur.setLength(0);
            if (i < len && line.charAt(i) == '"') {
                i++;
                boolean closed = false;
                while (i < len) {
                    char c = line.charAt(i);
                    if (c == '"') {
                        if (i + 1 < len && line.charAt(i + 1) == '"') {
                            cur.append('"');
                            i += 2;
                        } else {
                            i++;
                            closed = true;
                            break;
                        }
                    } else {
                        cur.append(c);
                        i++;
                    }
                }
                if (!closed) {
                    throw new IllegalArgumentException("unterminated quote");
                }
                if (i < len && line.charAt(i) != ',') {
                    throw new IllegalArgumentException("bad quote");
                }
            } else {
                while (i < len && line.charAt(i) != ',') {
                    if (line.charAt(i) == '"') {
                        throw new IllegalArgumentException("bad quote");
                    }
                    cur.append(line.charAt(i));
                    i++;
                }
            }
            fields.add(cur.toString());
            if (i >= len) {
                break;
            }
            i++;
            if (i == len) {
                fields.add("");
                break;
            }
        }
        return fields;
    }

    static int width(String s) {
        return s.codePointCount(0, s.length());
    }

    static String row(List<String> cells, int[] w) {
        StringBuilder sb = new StringBuilder();
        for (int c = 0; c < cells.size(); c++) {
            if (c > 0) {
                sb.append(" | ");
            }
            sb.append(cells.get(c));
            sb.append(" ".repeat(w[c] - width(cells.get(c))));
        }
        return sb.toString().stripTrailing();
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in, StandardCharsets.UTF_8));
        int n = Integer.parseInt(in.readLine().trim());
        List<List<String>> rows = new ArrayList<>();
        List<String> errors = new ArrayList<>();
        int cols = -1;
        for (int no = 1; no <= n; no++) {
            String line = in.readLine();
            if (line == null) {
                break;
            }
            try {
                List<String> fields = parse(line);
                if (cols < 0) {
                    cols = fields.size();
                }
                if (fields.size() != cols) {
                    errors.add("error line " + no + ": expected " + cols + " fields, got " + fields.size());
                } else {
                    rows.add(fields);
                }
            } catch (IllegalArgumentException e) {
                if (no == 1) {
                    System.out.println("error line 1: " + e.getMessage());
                    return;
                }
                errors.add("error line " + no + ": " + e.getMessage());
            }
        }
        if (rows.isEmpty()) {
            return;
        }
        int[] w = new int[cols];
        for (List<String> r : rows) {
            for (int c = 0; c < cols; c++) {
                w[c] = Math.max(w[c], width(r.get(c)));
            }
        }
        System.out.println(row(rows.get(0), w));
        StringBuilder sep = new StringBuilder();
        for (int c = 0; c < cols; c++) {
            if (c > 0) {
                sep.append("-+-");
            }
            sep.append("-".repeat(w[c]));
        }
        System.out.println(sep);
        for (int r = 1; r < rows.size(); r++) {
            System.out.println(row(rows.get(r), w));
        }
        errors.forEach(System.out::println);
    }
}
