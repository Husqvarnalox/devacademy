import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

public class Main {
    static <K> List<Map.Entry<K, Integer>> top(Map<K, Integer> counts, int k, Comparator<? super K> tie) {
        List<Map.Entry<K, Integer>> list = new ArrayList<>(counts.entrySet());
        list.sort((a, b) -> {
            int c = Integer.compare(b.getValue(), a.getValue());
            return c != 0 ? c : tie.compare(a.getKey(), b.getKey());
        });
        return list.subList(0, Math.min(k, list.size()));
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));
        int k = Integer.parseInt(in.readLine().trim());
        Map<String, Integer> counts = new HashMap<>();
        int total = 0;
        String line;
        while ((line = in.readLine()) != null) {
            for (String w : line.toLowerCase(Locale.ROOT).split("\\P{L}+")) {
                if (!w.isEmpty()) {
                    counts.merge(w, 1, Integer::sum);
                    total++;
                }
            }
        }
        for (Map.Entry<String, Integer> e : top(counts, k, Comparator.naturalOrder())) {
            System.out.println(e.getKey() + " " + e.getValue());
        }
        System.out.println("unique=" + counts.size());
        System.out.println("total=" + total);
    }
}
