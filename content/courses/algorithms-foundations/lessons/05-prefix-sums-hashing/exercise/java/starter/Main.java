import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.HashMap;
import java.util.StringTokenizer;

public class Main {
    // Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными). Суммы и ответ - long.
    static long countSegments(long[] a, long k) {
        // TODO: префиксные суммы + HashMap "сумма -> сколько раз встречалась", O(n)
        return 0;
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
