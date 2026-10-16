import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.HashMap;
import java.util.Map;
import java.util.TreeMap;

public class Main {
    static class AccountException extends Exception {
        AccountException(String message) {
            super(message);
        }
    }

    static class UnknownAccountException extends AccountException {
        UnknownAccountException(String name) {
            super("unknown account " + name);
        }
    }

    static class InsufficientFundsException extends AccountException {
        InsufficientFundsException(String name, long balance, long need) {
            super("insufficient funds: " + name + " has " + balance + ", need " + need);
        }
    }

    static final Map<String, Long> balances = new TreeMap<>();

    static long balance(String name) throws UnknownAccountException {
        Long b = balances.get(name);
        if (b == null) {
            throw new UnknownAccountException(name);
        }
        return b;
    }

    static long amount(String token, boolean allowZero) {
        if (!token.matches("[0-9]{1,12}") || (!allowZero && Long.parseLong(token) == 0)) {
            throw new IllegalArgumentException("invalid amount: " + token);
        }
        return Long.parseLong(token);
    }

    static class Tx implements AutoCloseable {
        private final Map<String, Long> before = new HashMap<>();
        private boolean committed;

        void change(String name, long delta) throws AccountException {
            long current = balance(name);
            if (current + delta < 0) {
                throw new InsufficientFundsException(name, current, -delta);
            }
            before.putIfAbsent(name, current);
            balances.put(name, current + delta);
        }

        void commit() {
            committed = true;
        }

        @Override
        public void close() {
            if (!committed) {
                before.forEach(balances::put);
                System.out.println("rollback");
            }
        }
    }

    static class Audit implements AutoCloseable {
        private final String line;

        Audit(String line) {
            this.line = line;
        }

        @Override
        public void close() {
            System.out.println("audit: " + line);
        }
    }

    static void execute(String line) throws AccountException {
        String[] t = line.trim().split("\\s+");
        switch (t[0]) {
            case "open" -> {
                need(t, 3);
                long initial = amount(t[2], true);
                if (balances.containsKey(t[1])) {
                    throw new AccountException("account exists: " + t[1]);
                }
                balances.put(t[1], initial);
            }
            case "deposit" -> {
                need(t, 3);
                long a = amount(t[2], false);
                try (Tx tx = new Tx()) {
                    tx.change(t[1], a);
                    tx.commit();
                }
            }
            case "withdraw" -> {
                need(t, 3);
                long a = amount(t[2], false);
                try (Tx tx = new Tx()) {
                    tx.change(t[1], -a);
                    tx.commit();
                }
            }
            case "transfer" -> {
                need(t, 4);
                long a = amount(t[3], false);
                try (Audit audit = new Audit(line.trim()); Tx tx = new Tx()) {
                    tx.change(t[1], -a);
                    tx.change(t[2], a);
                    tx.commit();
                }
            }
            default -> throw new IllegalArgumentException("unknown command: " + t[0]);
        }
    }

    static void need(String[] t, int count) {
        if (t.length != count) {
            throw new IllegalArgumentException("bad command");
        }
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));
        int n = Integer.parseInt(in.readLine().trim());
        for (int i = 0; i < n; i++) {
            String line = in.readLine();
            try {
                execute(line);
                System.out.println("ok");
            } catch (AccountException | IllegalArgumentException e) {
                System.out.println("error: " + e.getMessage());
            }
        }
        System.out.println("balances:");
        balances.forEach((k, v) -> System.out.println(k + "=" + v));
    }
}
