import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.Map;
import java.util.TreeMap;

public class Main {
    // TODO 1: объявите проверяемые исключения AccountException (extends Exception),
    //         UnknownAccountException и InsufficientFundsException (оба extends AccountException).
    // TODO 2: напишите класс Tx implements AutoCloseable. Он запоминает прежние балансы и,
    //         если commit() не вызвали, при close() восстанавливает их и печатает "rollback".
    // TODO 3: напишите класс Audit implements AutoCloseable, печатающий "audit: <команда>" в close().
    // TODO 4: в transfer откройте оба ресурса в одном try-with-resources: сначала Audit, затем Tx.
    static final Map<String, Long> balances = new TreeMap<>();

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));
        int n = Integer.parseInt(in.readLine().trim());
        for (int i = 0; i < n; i++) {
            String[] t = in.readLine().trim().split("\\s+");
            // Заготовка принимает только open и пока не проверяет ошибки.
            if (t[0].equals("open")) {
                balances.put(t[1], Long.parseLong(t[2]));
            }
            System.out.println("ok");
        }
        System.out.println("balances:");
        balances.forEach((k, v) -> System.out.println(k + "=" + v));
    }
}
