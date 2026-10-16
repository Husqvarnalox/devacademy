import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.Arrays;
import java.util.List;
import java.util.Locale;
import java.util.Map;
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

    // TODO: найдите документы, содержащие ВСЕ слова запроса без префикса "-" и НИ ОДНОГО слова
    // с префиксом "-" (регистр не важен). Результат - упорядоченное множество id.
    // Подсказка: Optional.ofNullable(index.get(слово)) избавляет от проверок на null.
    static Set<Integer> search(Map<String, Set<Integer>> index, String query) {
        return new TreeSet<>();
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));
        int d = Integer.parseInt(in.readLine().trim());
        List<Doc> docs = new java.util.ArrayList<>();
        for (int i = 0; i < d; i++) {
            docs.add(parseDoc(in.readLine()));
        }
        // TODO: постройте индекс слово -> упорядоченное множество id документов
        // (flatMap по словам документа и groupingBy с mapping)
        Map<String, Set<Integer>> index = new TreeMap<>();
        int q = Integer.parseInt(in.readLine().trim());
        for (int i = 0; i < q; i++) {
            String query = in.readLine();
            Set<Integer> ids = search(index, query);
            String text = ids.isEmpty() ? "none"
                    : ids.stream().map(String::valueOf).collect(Collectors.joining(" "));
            System.out.println(query + " -> " + text);
        }
        // TODO: слово, которое встречается в наибольшем числе документов (при равенстве - меньшее слово),
        // в формате "<слово> <число документов>"; если слов нет - "none". Используйте max и Optional.map/orElse.
        String top = "none";
        System.out.println("top=" + top);
    }
}
