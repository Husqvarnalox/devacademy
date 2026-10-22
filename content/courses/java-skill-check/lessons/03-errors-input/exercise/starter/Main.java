import java.util.*;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int ok = 0;
        int bad = 0;
        while (in.hasNextLine()) {
            String line = in.nextLine().trim();
            if (line.isEmpty()) {
                continue;
            }
            // TODO: разберите строку; принятую посчитайте в ok, остальные в bad
        }
        System.out.println("Принято: " + ok);
        System.out.println("Пропущено: " + bad);
        System.out.printf(Locale.ROOT, "Средний балл: %.2f%n", 0.0);
        // TODO: если есть принятые, напечатайте "Лучший: <имя> (<балл>)"
    }
}
