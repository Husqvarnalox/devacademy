import java.util.*;

public class Main {
    // Массив из двух элементов: наименьшее и наибольшее значение
    static int[] minMax(int[] a) {
        int min = a[0];
        int max = a[0];
        for (int x : a) {
            if (x < min) {
                min = x;
            }
            if (x > max) {
                max = x;
            }
        }
        return new int[] {min, max};
    }

    // Новый список без всех элементов, равных value
    static List<Integer> removeValue(List<Integer> xs, int value) {
        List<Integer> result = new ArrayList<>();
        for (int x : xs) {
            if (x != value) {
                result.add(x);
            }
        }
        return result;
    }

    // Слово -> количество вхождений (без учёта регистра)
    static Map<String, Integer> countWords(String line) {
        Map<String, Integer> counts = new HashMap<>();
        String text = line.trim();
        if (text.isEmpty()) {
            return counts;
        }
        for (String w : text.toLowerCase().split("\\s+")) {
            counts.merge(w, 1, Integer::sum);
        }
        return counts;
    }

    // Заглавные первые буквы слов с точками: "ivan petrov" -> "I.P."
    static String initials(String fullName) {
        String text = fullName.trim();
        if (text.isEmpty()) {
            return "";
        }
        String result = "";
        for (String w : text.split("\\s+")) {
            result += Character.toUpperCase(w.charAt(0)) + ".";
        }
        return result;
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int n = in.nextInt();
        int[] a = new int[n];
        List<Integer> list = new ArrayList<>();
        for (int i = 0; i < n; i++) {
            a[i] = in.nextInt();
            list.add(a[i]);
        }
        int value = in.nextInt();
        in.nextLine();
        String text = in.nextLine();
        String name = in.nextLine();
        int[] mm = minMax(a);
        System.out.println("Мин/макс: " + mm[0] + " " + mm[1]);
        System.out.println("Без " + value + ": " + removeValue(list, value));
        System.out.println("Слова: " + new TreeMap<>(countWords(text)));
        System.out.println("Инициалы: " + initials(name));
    }
}
