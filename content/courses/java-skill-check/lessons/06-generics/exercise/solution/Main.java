import java.util.*;

public class Main {
    static <T extends Comparable<T>> T maxOf(List<T> xs) {
        T best = xs.get(0);
        for (T x : xs) {
            if (x.compareTo(best) > 0) {
                best = x;
            }
        }
        return best;
    }

    static double sum(List<? extends Number> xs) {
        double s = 0;
        for (Number n : xs) {
            s += n.doubleValue();
        }
        return s;
    }

    static <T> void copyInto(List<? super T> dst, List<? extends T> src) {
        for (T x : src) {
            dst.add(x);
        }
    }

    static <K, V extends Comparable<V>> K keyOfMax(Map<K, V> m) {
        K bestKey = null;
        V bestValue = null;
        for (Map.Entry<K, V> e : m.entrySet()) {
            if (bestValue == null || e.getValue().compareTo(bestValue) > 0) {
                bestKey = e.getKey();
                bestValue = e.getValue();
            }
        }
        return bestKey;
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
