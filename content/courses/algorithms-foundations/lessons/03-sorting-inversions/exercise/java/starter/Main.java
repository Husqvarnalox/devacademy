import java.io.BufferedInputStream;
import java.io.IOException;
import java.io.StreamTokenizer;

public class Main {
    // Сортирует a[lo, hi) на месте (tmp - буфер) и возвращает число инверсий; результат до 5e9+ - нужен long.
    static long sortCount(int[] a, int[] tmp, int lo, int hi) {
        // TODO: сортировка слиянием, которая заодно считает инверсии (O(n log n))
        return 0;
    }

    public static void main(String[] args) throws IOException {
        StreamTokenizer in = new StreamTokenizer(new BufferedInputStream(System.in));
        in.nextToken();
        int n = (int) in.nval;
        int[] a = new int[n];
        for (int i = 0; i < n; i++) {
            in.nextToken();
            a[i] = (int) in.nval; // |a_i| <= 1e9 помещается в int
        }
        System.out.println(sortCount(a, new int[n], 0, n));
    }
}
