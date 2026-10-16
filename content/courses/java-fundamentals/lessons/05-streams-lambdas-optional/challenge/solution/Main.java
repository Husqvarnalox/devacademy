import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.Arrays;
import java.util.Comparator;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.TreeMap;
import java.util.TreeSet;
import java.util.stream.Collectors;

public class Main {
    record Doc(int id, Set<String> words) {}

    static Set<String> words(String text) {
        return Arrays.stream(text.toLowerCase(Locale.ROOT).split("[^\\p{L}\\p{Nd}]+"))
                .filter(s -> !s.isEmpty())
                .collect(Collectors.toCollection(TreeSet::new));
    }

    static Doc parseDoc(String line) {
        int colon = line.indexOf(':');
        return new Doc(Integer.parseInt(line.substring(0, colon).trim()), words(line.substring(colon + 1)));
    }

    static Set<Integer> search(Map<String, Set<Integer>> index, String query) {
        List<String> tokens = Arrays.stream(query.trim().split("\\s+")).filter(t -> !t.isEmpty() && !t.equals("-")).toList();
        List<String> include = tokens.stream().filter(t -> !t.startsWith("-")).map(t -> t.toLowerCase(Locale.ROOT)).toList();
        List<String> exclude = tokens.stream().filter(t -> t.startsWith("-")).map(t -> t.substring(1).toLowerCase(Locale.ROOT)).toList();
        Set<Integer> result = include.stream()
                .map(w -> Optional.ofNullable(index.get(w)).orElse(Set.of()))
                .<Set<Integer>>map(TreeSet::new)
                .reduce((a, b) -> {
                    a.retainAll(b);
                    return a;
                })
                .orElseGet(TreeSet::new);
        exclude.forEach(w -> result.removeAll(index.getOrDefault(w, Set.of())));
        return result;
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));
        int d = Integer.parseInt(in.readLine().trim());
        List<Doc> docs = new java.util.ArrayList<>();
        for (int i = 0; i < d; i++) {
            docs.add(parseDoc(in.readLine()));
        }
        Map<String, Set<Integer>> index = docs.stream()
                .flatMap(doc -> doc.words().stream().map(w -> Map.entry(w, doc.id())))
                .collect(Collectors.groupingBy(Map.Entry::getKey, TreeMap::new,
                        Collectors.mapping(Map.Entry::getValue, Collectors.toCollection(TreeSet::new))));
        int q = Integer.parseInt(in.readLine().trim());
        for (int i = 0; i < q; i++) {
            String query = in.readLine();
            Set<Integer> ids = search(index, query);
            String text = ids.isEmpty() ? "none"
                    : ids.stream().map(String::valueOf).collect(Collectors.joining(" "));
            System.out.println(query + " -> " + text);
        }
        String top = index.entrySet().stream()
                .max(Comparator.comparingInt((Map.Entry<String, Set<Integer>> e) -> e.getValue().size())
                        .thenComparing(Map.Entry::getKey, Comparator.reverseOrder()))
                .map(e -> e.getKey() + " " + e.getValue().size())
                .orElse("none");
        System.out.println("top=" + top);
    }
}
