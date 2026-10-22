import java.util.*;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int ok = 0;
        int bad = 0;
        int total = 0;
        String bestName = "";
        int bestScore = -1;
        while (in.hasNextLine()) {
            String line = in.nextLine().trim();
            if (line.isEmpty()) {
                continue;
            }
            String[] parts = line.split(";", -1);
            if (parts.length != 2 || parts[0].trim().isEmpty()) {
                bad++;
                continue;
            }
            int score;
            try {
                score = Integer.parseInt(parts[1].trim());
            } catch (NumberFormatException e) {
                bad++;
                continue;
            }
            if (score < 0 || score > 100) {
                bad++;
                continue;
            }
            ok++;
            total += score;
            if (score > bestScore) {
                bestScore = score;
                bestName = parts[0].trim();
            }
        }
        System.out.println("Принято: " + ok);
        System.out.println("Пропущено: " + bad);
        System.out.printf(Locale.ROOT, "Средний балл: %.2f%n", ok == 0 ? 0.0 : (double) total / ok);
        if (ok > 0) {
            System.out.println("Лучший: " + bestName + " (" + bestScore + ")");
        }
    }
}
