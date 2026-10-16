import java.io.BufferedInputStream;
import java.io.IOException;
import java.io.StreamTokenizer;

public class Main {
    // Сколько кусков длины length получится из всех досок (сумма выходит за int, поэтому long).
    static long pieces(long[] a, long length) {
        long total = 0;
        for (long x : a) total += x / length;
        return total;
    }

    // Наибольшая целая длина L >= 1, при которой выходит не менее k кусков; 0, если такой L нет.
    static long longestPiece(long[] a, long k) {
        long mx = 0;
        for (long x : a) mx = Math.max(mx, x);
        long lo = 0, hi = mx + 1; // инвариант: lo подходит (0 - условно), hi не подходит
        while (hi - lo > 1) {
            long mid = lo + (hi - lo) / 2;
            if (pieces(a, mid) >= k) lo = mid;
            else hi = mid;
        }
        return lo;
    }

    public static void main(String[] args) throws IOException {
        StreamTokenizer in = new StreamTokenizer(new BufferedInputStream(System.in));
        in.nextToken();
        int n = (int) in.nval;
        in.nextToken();
        long k = (long) in.nval; // k <= 1e12 точно представимо в double
        long[] a = new long[n];
        for (int i = 0; i < n; i++) {
            in.nextToken();
            a[i] = (long) in.nval;
        }
        System.out.println(longestPiece(a, k));
    }
}
