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
        // TODO: сгруппируйте сотрудников по отделу в TreeMap (groupingBy) и для каждого отдела
        // выведите: <отдел>: count=<число> avg=<средняя зарплата, 2 знака> top=<имя> <зарплата>
        // Среди равных зарплат top - сотрудник с меньшим именем.
        Map<String, List<Employee>> byDept = new TreeMap<>();
        System.out.println("departments=" + byDept.size());
        long total = 0;
        System.out.println("total=" + total);
    }
}
