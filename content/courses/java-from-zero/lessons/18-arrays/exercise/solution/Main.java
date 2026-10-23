import java.util.Locale;
import java.util.Scanner;

public class Main {
    // Наибольший элемент массива (в массиве есть хотя бы один элемент)
    static int maxValue(int[] a) {
        int max = a[0];
        for (int x : a) {
            if (x > max) {
                max = x;
            }
        }
        return max;
    }

    // Среднее арифметическое элементов как double
    static double average(int[] a) {
        int sum = 0;
        for (int x : a) {
            sum += x;
        }
        return (double) sum / a.length;
    }

    // Сколько элементов строго больше limit
    static int countAbove(int[] a, int limit) {
        int count = 0;
        for (int x : a) {
            if (x > limit) {
                count++;
            }
        }
        return count;
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
