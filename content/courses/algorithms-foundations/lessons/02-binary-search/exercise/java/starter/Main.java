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
        // TODO: бинарный поиск по ответу - длине куска (O(n log max(a)))
        return 0;
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
