import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import java.util.regex.Pattern;

public class Main {
    private static final Pattern NAME = Pattern.compile("[A-Za-z0-9_.-]+");
    private static final String GLOBAL = "global";

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in, StandardCharsets.UTF_8));
        int n = Integer.parseInt(in.readLine().trim());
        Map<String, Map<String, String>> sections = new TreeMap<>();
        List<String> warnings = new ArrayList<>();
        String current = GLOBAL;
        for (int no = 1; no <= n; no++) {
            String line = in.readLine();
            if (line == null) {
                break;
            }
            String text = line.strip();
            if (text.isEmpty() || text.startsWith("#") || text.startsWith(";")) {
                continue;
            }
            if (text.startsWith("[")) {
                String name = text.endsWith("]") ? text.substring(1, text.length() - 1).strip() : "";
                if (!NAME.matcher(name).matches()) {
                    warnings.add("invalid line " + no + ": " + text);
                    continue;
                }
                current = name;
                sections.computeIfAbsent(current, k -> new TreeMap<>());
                continue;
            }
            int eq = text.indexOf('=');
            String key = eq < 0 ? "" : text.substring(0, eq).strip();
            if (!NAME.matcher(key).matches()) {
                warnings.add("invalid line " + no + ": " + text);
                continue;
            }
            String value = text.substring(eq + 1).strip();
            if (value.length() >= 2 && value.startsWith("\"") && value.endsWith("\"")) {
                value = value.substring(1, value.length() - 1);
            }
            sections.computeIfAbsent(current, k -> new TreeMap<>()).put(key, value);
        }
        warnings.forEach(System.out::println);
        int keys = 0;
        List<String> names = new ArrayList<>(sections.keySet());
        if (names.remove(GLOBAL)) {
            names.add(0, GLOBAL);
        }
        for (String name : names) {
            System.out.println("[" + name + "]");
            for (Map.Entry<String, String> e : sections.get(name).entrySet()) {
                System.out.println(e.getKey() + "=<" + e.getValue() + ">");
                keys++;
            }
        }
        System.out.println("sections=" + names.size() + " keys=" + keys);
    }
}
