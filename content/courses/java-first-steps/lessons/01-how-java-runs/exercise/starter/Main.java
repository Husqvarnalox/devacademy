import java.util.Locale;
import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int n = sc.nextInt();
        long sum = 0;
        long min = 0;
        long max = 0;
        // TODO: прочитайте n чисел (long) и вычислите сумму, минимум и максимум
        System.out.printf(Locale.ROOT, "sum=%d%nmin=%d%nmax=%d%navg=%.2f%n", sum, min, max, 0.0);
    }
}
