import java.util.*;

public class Main {
    // Массив из двух элементов: наименьшее и наибольшее значение
    static int[] minMax(int[] a) {
        return new int[] {0, 0}; // TODO
    }

    // Новый список без всех элементов, равных value
    static List<Integer> removeValue(List<Integer> xs, int value) {
        return xs; // TODO
    }

    // Слово -> количество вхождений (без учёта регистра)
    static Map<String, Integer> countWords(String line) {
        return new HashMap<>(); // TODO
    }

    // Заглавные первые буквы слов с точками: "ivan petrov" -> "I.P."
    static String initials(String fullName) {
        return ""; // TODO
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
