import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Scanner;
import java.util.concurrent.Callable;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;

public class Main {
    record ChunkResult(int count, long sum) {}

    public static void main(String[] args) throws Exception {
        Scanner sc = new Scanner(System.in);
        int k = sc.nextInt();
        int n = sc.nextInt();
        long[] data = new long[n];
        for (int i = 0; i < n; i++) {
            data[i] = sc.nextLong();
        }
        Map<Integer, Integer> mods = new ConcurrentHashMap<>();
        List<Future<ChunkResult>> futures = new ArrayList<>();
        try (ExecutorService pool = Executors.newFixedThreadPool(k)) {
            int from = 0;
            for (int i = 0; i < k; i++) {
                int size = n / k + (i < n % k ? 1 : 0);
                int start = from;
                int end = from + size;
                from = end;
                Callable<ChunkResult> task = () -> {
                    long sum = 0;
                    for (int j = start; j < end; j++) {
                        sum += data[j];
                        mods.merge((int) Math.floorMod(data[j], 3L), 1, Integer::sum);
                    }
                    return new ChunkResult(end - start, sum);
                };
                futures.add(pool.submit(task));
            }
        }
        long total = 0;
        int largest = 0;
        long best = Long.MIN_VALUE;
        for (int i = 0; i < k; i++) {
            ChunkResult r;
            try {
                r = futures.get(i).get();
            } catch (ExecutionException e) {
                throw new IllegalStateException(e.getCause());
            }
            System.out.println("chunk " + i + ": count=" + r.count() + " sum=" + r.sum());
            total += r.sum();
            if (r.sum() > best) {
                best = r.sum();
                largest = i;
            }
        }
        System.out.println("total=" + total);
        System.out.println("largest=" + largest);
        System.out.println("mod0=" + mods.getOrDefault(0, 0) + " mod1=" + mods.getOrDefault(1, 0) + " mod2=" + mods.getOrDefault(2, 0));
    }
}
