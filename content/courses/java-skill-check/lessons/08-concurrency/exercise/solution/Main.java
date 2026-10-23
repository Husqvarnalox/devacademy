import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicInteger;

public class Main {
    static long parallelSum(List<Integer> nums, int parts) throws Exception {
        ExecutorService pool = Executors.newFixedThreadPool(Math.max(1, parts));
        try {
            int n = nums.size();
            int chunk = (n + parts - 1) / parts;
            List<Future<Long>> futures = new ArrayList<>();
            for (int from = 0; from < n; from += chunk) {
                List<Integer> part = nums.subList(from, Math.min(n, from + chunk));
                futures.add(pool.submit(() -> {
                    long s = 0;
                    for (int x : part) {
                        s += x;
                    }
                    return s;
                }));
            }
            long total = 0;
            for (Future<Long> f : futures) {
                total += f.get();
            }
            return total;
        } finally {
            pool.shutdown();
        }
    }

    static int countWithThreads(int threads, int perThread) throws Exception {
        AtomicInteger counter = new AtomicInteger();
        List<Thread> list = new ArrayList<>();
        for (int i = 0; i < threads; i++) {
            Thread t = new Thread(() -> {
                for (int k = 0; k < perThread; k++) {
                    counter.incrementAndGet();
                }
            });
            list.add(t);
            t.start();
        }
        for (Thread t : list) {
            t.join();
        }
        return counter.get();
    }

    public static void main(String[] args) throws Exception {
        Scanner in = new Scanner(System.in);
        int parts = in.nextInt();
        int threads = in.nextInt();
        int perThread = in.nextInt();
        int n = in.nextInt();
        List<Integer> nums = new ArrayList<>();
        for (int i = 0; i < n; i++) {
            nums.add(in.nextInt());
        }
        System.out.println("Сумма: " + parallelSum(nums, parts));
        System.out.println("Счётчик: " + countWithThreads(threads, perThread));
    }
}
