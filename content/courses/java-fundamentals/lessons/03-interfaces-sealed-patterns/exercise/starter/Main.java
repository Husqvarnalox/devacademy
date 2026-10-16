import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.Locale;

public class Main {
    sealed interface Shape permits Circle, Rect, Triangle {}

    record Circle(int r) implements Shape {}

    record Rect(int w, int h) implements Shape {}

    record Triangle(int a, int b, int c) implements Shape {}

    // TODO: реализуйте area, perimeter и kind через switch по sealed-интерфейсу
    // с шаблонами записей, без ветки default.
    static double area(Shape s) {
        return 0;
    }

    static double perimeter(Shape s) {
        return 0;
    }

    static String kind(Shape s) {
        return s.getClass().getSimpleName().toLowerCase(Locale.ROOT);
    }

    static Shape parse(String line) {
        String[] t = line.trim().split("\\s+");
        try {
            int[] v = new int[t.length - 1];
            for (int i = 1; i < t.length; i++) {
                v[i - 1] = Integer.parseInt(t[i]);
                if (v[i - 1] < 1) {
                    return null;
                }
            }
            return switch (t[0]) {
                case "circle" -> v.length == 1 ? new Circle(v[0]) : null;
                case "rect" -> v.length == 2 ? new Rect(v[0], v[1]) : null;
                case "tri" -> v.length == 3 && v[0] + v[1] > v[2] && v[1] + v[2] > v[0] && v[0] + v[2] > v[1]
                        ? new Triangle(v[0], v[1], v[2]) : null;
                default -> null;
            };
        } catch (NumberFormatException e) {
            return null;
        }
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));
        int n = Integer.parseInt(in.readLine().trim());
        double total = 0;
        for (int i = 0; i < n; i++) {
            String line = in.readLine();
            Shape s = parse(line);
            if (s == null) {
                System.out.println("invalid: " + line);
                continue;
            }
            total += area(s);
            System.out.printf(Locale.ROOT, "%s area=%.2f perimeter=%.2f%n", kind(s), area(s), perimeter(s));
        }
        System.out.printf(Locale.ROOT, "total=%.2f%n", total);
    }
}
