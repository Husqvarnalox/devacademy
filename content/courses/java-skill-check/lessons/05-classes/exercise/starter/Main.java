import java.util.*;

public class Main {
    enum Level {
        LOW, MEDIUM, HIGH;

        // TODO: LOW для суммы меньше 100, MEDIUM меньше 1000, иначе HIGH
        static Level of(int amount) {
            return LOW;
        }
    }

    // TODO: в компактном конструкторе бросьте IllegalArgumentException("Отрицательная сумма: " + amount)
    record Order(String id, int amount) {
        Level level() {
            return Level.LOW;
        }
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        Set<Order> seen = new HashSet<>();
        Map<Level, Integer> counts = new EnumMap<>(Level.class);
        int rejected = 0;
        while (in.hasNextLine()) {
            String line = in.nextLine().trim();
            if (line.isEmpty()) {
                continue;
            }
            String[] p = line.split(" ");
            try {
                Order o = new Order(p[0], Integer.parseInt(p[1]));
                if (!seen.add(o)) {
                    System.out.println("повтор: " + o.id());
                    continue;
                }
                counts.merge(o.level(), 1, Integer::sum);
            } catch (IllegalArgumentException e) {
                rejected++;
                System.out.println("Отклонён: " + e.getMessage());
            }
        }
        for (Level l : Level.values()) {
            System.out.println(l + ": " + counts.getOrDefault(l, 0));
        }
        System.out.println("Отклонено: " + rejected);
    }
}
