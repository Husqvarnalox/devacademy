import java.io.IOException;
import java.io.StreamTokenizer;
import java.io.BufferedInputStream;

public class Main {
    // Максимальная сумма непустого непрерывного отрезка. Суммы выходят за int, поэтому long.
    static long bestSegment(long[] a) {
        long best = a[0], cur = a[0];
        for (int i = 1; i < a.length; i++) {
            cur = Math.max(a[i], cur + a[i]);
            best = Math.max(best, cur);
        }
        return best;
    }

    public static void main(String[] args) throws IOException {
        StreamTokenizer in = new StreamTokenizer(new BufferedInputStream(System.in));
        in.nextToken();
        int n = (int) in.nval;
        long[] a = new long[n];
        for (int i = 0; i < n; i++) {
            in.nextToken();
            a[i] = (long) in.nval; // |значение| <= 1e9 точно представимо в double
        }
        System.out.println(bestSegment(a));
    }
}
