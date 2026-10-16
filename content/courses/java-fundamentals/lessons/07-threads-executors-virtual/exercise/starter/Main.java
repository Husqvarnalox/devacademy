import java.util.Scanner;

public class Main {
    public static void main(String[] args) throws Exception {
        Scanner sc = new Scanner(System.in);
        int k = sc.nextInt();
        int n = sc.nextInt();
        long[] data = new long[n];
        for (int i = 0; i < n; i++) {
            data[i] = sc.nextLong();
        }
        // TODO: разбейте data на k подряд идущих частей: часть i содержит n / k элементов
        // и ещё один, если i < n % k. Каждую часть обработайте задачей Callable в
        // ExecutorService (Executors.newFixedThreadPool(k)); результаты получайте через Future.get()
        // в порядке номеров частей. Счётчики по остаткам от деления на 3 (Math.floorMod)
        // накапливайте в общем ConcurrentHashMap через merge.
        // Ожидаемый формат вывода:
        //   chunk <i>: count=<число элементов> sum=<сумма>   (для каждой части)
        //   total=<общая сумма>
        //   largest=<номер части с наибольшей суммой, при равенстве - наименьший номер>
        //   mod0=<..> mod1=<..> mod2=<..>
        long sum = 0;
        for (long x : data) {
            sum += x;
        }
        System.out.println("total=" + sum);
    }
}
