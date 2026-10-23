import java.util.*;

public class Main {
    // Наибольший элемент (при равенстве - первый); список непустой
    static <T extends Comparable<T>> T maxOf(List<T> xs) {
        return null; // TODO
    }

    // Сумма элементов как double
    static double sum(List<? extends Number> xs) {
        return 0; // TODO
    }

    // Добавляет все элементы src в конец dst
    static <T> void copyInto(List<? super T> dst, List<? extends T> src) {
        // TODO
    }

    // Ключ с наибольшим значением (при равенстве - первый в порядке обхода)
    static <K, V extends Comparable<V>> K keyOfMax(Map<K, V> m) {
        return null; // TODO
    }

    static List<String> tokens(String line) {
        line = line.trim();
        return line.isEmpty() ? new ArrayList<>() : new ArrayList<>(Arrays.asList(line.split("\\s+")));
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        String first = in.hasNextLine() ? in.nextLine() : "";
        String second = in.hasNextLine() ? in.nextLine() : "";
        List<Integer> nums = new ArrayList<>();
        for (String t : tokens(first)) {
            nums.add(Integer.parseInt(t));
        }
        List<String> words = tokens(second);
        System.out.println("Сумма: " + sum(nums));
        if (!nums.isEmpty()) {
            System.out.println("Максимум чисел: " + maxOf(nums));
        }
        if (!words.isEmpty()) {
            System.out.println("Максимум слов: " + maxOf(words));
        }
        List<Object> all = new ArrayList<>();
        copyInto(all, nums);
        copyInto(all, words);
        System.out.println("Всё вместе: " + all);
        Map<String, Integer> lengths = new TreeMap<>();
        for (String w : words) {
            lengths.put(w, w.length());
        }
        System.out.println("Самое длинное: " + (lengths.isEmpty() ? "нет" : keyOfMax(lengths)));
    }
}
