import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.HashMap;
import java.util.StringTokenizer;

public class Main {
    // Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными). Суммы и ответ - long.
    static long countSegments(long[] a, long k) {
        HashMap<Long, Integer> seen = new HashMap<>(); // префиксная сумма -> сколько раз встречалась
        seen.put(0L, 1);                               // пустой префикс
        long cur = 0, ans = 0;
        for (long x : a) {
            cur += x;
            ans += seen.getOrDefault(cur - k, 0);      // левые границы, для которых сумма отрезка ровно k
            seen.merge(cur, 1, Integer::sum);
        }
        return ans;
    }

    public static void main(String[] args) throws IOException {
        BufferedReader br = new BufferedReader(new InputStreamReader(System.in));
        StringTokenizer st = new StringTokenizer(br.readLine());
        int n = Integer.parseInt(st.nextToken());
        long k = Long.parseLong(st.nextToken());
        long[] a = new long[n];
        st = new StringTokenizer(br.readLine());
        for (int i = 0; i < n; i++) a[i] = Long.parseLong(st.nextToken());
        System.out.println(countSegments(a, k));
    }
}
