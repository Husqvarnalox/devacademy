import java.util.*;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        String line = in.hasNextLine() ? in.nextLine().trim() : "";
        if (line.isEmpty()) {
            System.out.println("Слов нет");
            return;
        }
        List<String> order = new ArrayList<>();
        Map<String, Integer> counts = new HashMap<>();
        for (String w : line.toLowerCase().split("\\s+")) {
            if (!counts.containsKey(w)) {
                order.add(w);
            }
            counts.merge(w, 1, Integer::sum);
        }
        String best = order.get(0);
        for (String w : order) {
            System.out.println(w + ": " + counts.get(w));
            if (counts.get(w) > counts.get(best)) {
                best = w;
            }
        }
        System.out.println("Самое частое: " + best);
    }
}
