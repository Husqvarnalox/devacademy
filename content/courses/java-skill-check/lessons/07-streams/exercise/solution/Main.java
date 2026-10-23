import java.util.*;
import java.util.stream.*;

public class Main {
    record Employee(String name, String dept, int salary) {}

    static List<String> namesAbove(List<Employee> employees, int min) {
        return employees.stream()
                .filter(e -> e.salary() >= min)
                .map(Employee::name)
                .sorted()
                .toList();
    }

    static Map<String, Integer> totalByDept(List<Employee> employees) {
        return employees.stream()
                .collect(Collectors.groupingBy(Employee::dept, TreeMap::new, Collectors.summingInt(Employee::salary)));
    }

    static Optional<Employee> topEarner(List<Employee> employees) {
        return employees.stream().max(Comparator.comparingInt(Employee::salary));
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int min = in.hasNextLine() ? Integer.parseInt(in.nextLine().trim()) : 0;
        List<Employee> employees = new ArrayList<>();
        while (in.hasNextLine()) {
            String line = in.nextLine().trim();
            if (line.isEmpty()) {
                continue;
            }
            String[] p = line.split(" ");
            employees.add(new Employee(p[0], p[1], Integer.parseInt(p[2])));
        }
        System.out.println("Не ниже " + min + ": " + namesAbove(employees, min));
        System.out.println("По отделам: " + totalByDept(employees));
        Optional<Employee> top = topEarner(employees);
        System.out.println("Лучший: " + top.map(e -> e.name() + " (" + e.salary() + ")").orElse("нет"));
    }
}
