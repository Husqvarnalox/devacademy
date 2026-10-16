import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Optional;
import java.util.TreeMap;
import java.util.stream.Collectors;

public class Main {
    record Employee(String name, String dept, int salary) {}

    static Optional<Employee> parse(String line) {
        String[] t = line.trim().split("\\s+");
        if (t.length != 3 || !t[2].matches("[0-9]{1,9}")) {
            return Optional.empty();
        }
        return Optional.of(new Employee(t[0], t[1], Integer.parseInt(t[2])));
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));
        int n = Integer.parseInt(in.readLine().trim());
        List<Employee> all = new ArrayList<>();
        for (int i = 0; i < n; i++) {
            String line = in.readLine();
            parse(line).ifPresentOrElse(all::add, () -> System.out.println("invalid: " + line));
        }
        Map<String, List<Employee>> byDept = all.stream()
                .collect(Collectors.groupingBy(Employee::dept, TreeMap::new, Collectors.toList()));
        byDept.forEach((dept, list) -> {
            double avg = list.stream().mapToInt(Employee::salary).average().orElse(0);
            Employee top = list.stream()
                    .max(Comparator.comparingInt(Employee::salary).thenComparing(Employee::name, Comparator.reverseOrder()))
                    .orElseThrow();
            System.out.printf(Locale.ROOT, "%s: count=%d avg=%.2f top=%s %d%n", dept, list.size(), avg, top.name(), top.salary());
        });
        long total = all.stream().mapToLong(Employee::salary).sum();
        System.out.println("total=" + total);
    }
}
