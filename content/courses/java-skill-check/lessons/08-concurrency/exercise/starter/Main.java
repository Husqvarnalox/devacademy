import java.util.*;
import java.util.concurrent.*;
import java.util.concurrent.atomic.AtomicInteger;

public class Main {
    // Сумма элементов: список делится на parts частей, каждая считается задачей пула
    static long parallelSum(List<Integer> nums, int parts) throws Exception {
        return 0; // TODO
    }

    // threads потоков увеличивают общий счётчик по perThread раз; вернуть итог
    static int countWithThreads(int threads, int perThread) throws Exception {
        return 0; // TODO
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
