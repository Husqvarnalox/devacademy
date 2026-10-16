import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.StringTokenizer;

public class Main {
    // Число непустых непрерывных отрезков a с суммой не больше limit (все a[i] > 0). Суммы и ответ - long.
    static long countSegments(long[] a, long limit) {
        long total = 0, cur = 0;
        int left = 0; // окно a[left..right]; инвариант: его сумма cur <= limit (или окно пусто)
        for (int right = 0; right < a.length; right++) {
            cur += a[right];
            while (cur > limit) {
                cur -= a[left];
                left++;
            }
            total += right - left + 1; // подходящие отрезки, оканчивающиеся в right
        }
        return total;
    }

    public static void main(String[] args) throws IOException {
        BufferedReader br = new BufferedReader(new InputStreamReader(System.in));
        StringTokenizer st = new StringTokenizer(br.readLine());
        int n = Integer.parseInt(st.nextToken());
        long limit = Long.parseLong(st.nextToken());
        long[] a = new long[n];
        st = new StringTokenizer(br.readLine());
        for (int i = 0; i < n; i++) a[i] = Long.parseLong(st.nextToken());
        System.out.println(countSegments(a, limit));
    }
}
