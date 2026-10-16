import java.util.Locale;
import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int n = sc.nextInt();
        long sum = 0;
        long min = Long.MAX_VALUE;
        long max = Long.MIN_VALUE;
        for (int i = 0; i < n; i++) {
            long x = sc.nextLong();
            sum += x;
            min = Math.min(min, x);
            max = Math.max(max, x);
        }
        System.out.printf(Locale.ROOT, "sum=%d%nmin=%d%nmax=%d%navg=%.2f%n", sum, min, max, (double) sum / n);
    }
}
