import java.util.Locale;
import java.util.Scanner;

public class Main {
    // Наибольший элемент массива (в массиве есть хотя бы один элемент)
    static int maxValue(int[] a) {
        return 0; // TODO
    }

    // Среднее арифметическое элементов как double
    static double average(int[] a) {
        return 0.0; // TODO
    }

    // Сколько элементов строго больше limit
    static int countAbove(int[] a, int limit) {
        return 0; // TODO
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int n = in.nextInt();
        int[] temps = new int[n];
        for (int i = 0; i < n; i++) {
            temps[i] = in.nextInt();
        }
        int limit = in.nextInt();
        System.out.println("Максимум: " + maxValue(temps));
        System.out.printf(Locale.ROOT, "Среднее: %.2f%n", average(temps));
        System.out.println("Выше " + limit + ": " + countAbove(temps, limit));
    }
}
