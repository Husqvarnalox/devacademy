import java.io.BufferedInputStream;
import java.io.IOException;
import java.io.StreamTokenizer;

public class Main {
    // Сортирует a[lo, hi) на месте (tmp - буфер) и возвращает число инверсий; результат до 5e9+ - нужен long.
    static long sortCount(int[] a, int[] tmp, int lo, int hi) {
        if (hi - lo <= 1) return 0;
        int mid = (lo + hi) >>> 1;
        long inv = sortCount(a, tmp, lo, mid) + sortCount(a, tmp, mid, hi);
        int i = lo, j = mid, k = lo;
        while (i < mid && j < hi) {
            if (a[i] <= a[j]) {
                tmp[k++] = a[i++];
            } else {
                tmp[k++] = a[j++];
                inv += mid - i; // a[j] меньше всех оставшихся слева
            }
        }
        while (i < mid) tmp[k++] = a[i++];
        while (j < hi) tmp[k++] = a[j++];
        System.arraycopy(tmp, lo, a, lo, hi - lo);
        return inv;
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
