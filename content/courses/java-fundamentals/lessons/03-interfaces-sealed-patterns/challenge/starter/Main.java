import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.ArrayDeque;
import java.util.Deque;
import java.util.HashMap;
import java.util.Map;

public class Main {
    sealed interface Expr permits Num, Var, Add, Sub, Mul, Neg {}

    record Num(long v) implements Expr {}

    record Var(String name) implements Expr {}

    record Add(Expr l, Expr r) implements Expr {}

    record Sub(Expr l, Expr r) implements Expr {}

    record Mul(Expr l, Expr r) implements Expr {}

    record Neg(Expr e) implements Expr {}

    static final class Unbound extends RuntimeException {
        Unbound(String name) {
            super(name);
        }
    }

    static Expr parse(Deque<String> t) {
        if (t.isEmpty()) {
            throw new IllegalArgumentException("eof");
        }
        String s = t.poll();
        return switch (s) {
            case "+" -> new Add(parse(t), parse(t));
            case "-" -> new Sub(parse(t), parse(t));
            case "*" -> new Mul(parse(t), parse(t));
            case "neg" -> new Neg(parse(t));
            default -> {
                if (s.matches("-?[0-9]+")) {
                    try {
                        yield new Num(Long.parseLong(s));
                    } catch (NumberFormatException e) {
                        throw new IllegalArgumentException("literal");
                    }
                }
                if (s.matches("[a-z]")) {
                    yield new Var(s);
                }
                throw new IllegalArgumentException("token");
            }
        };
    }

    // TODO: реализуйте show, eval и simplify через switch с шаблонами записей (без default).
    // Сейчас show печатает имя типа, eval возвращает 0, а simplify ничего не упрощает.
    static String show(Expr e) {
        return e.getClass().getSimpleName();
    }

    static long eval(Expr e, Map<String, Long> env) {
        return 0;
    }

    static Expr simplify(Expr e) {
        return e;
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));
        int m = Integer.parseInt(in.readLine().trim());
        Map<String, Long> env = new HashMap<>();
        for (int i = 0; i < m; i++) {
            String[] p = in.readLine().trim().split("\\s+");
            env.put(p[0], Long.parseLong(p[1]));
        }
        int n = Integer.parseInt(in.readLine().trim());
        for (int i = 0; i < n; i++) {
            String line = in.readLine();
            Expr e;
            try {
                Deque<String> tokens = new ArrayDeque<>(java.util.Arrays.asList(line.trim().split("\\s+")));
                e = parse(tokens);
                if (!tokens.isEmpty()) {
                    throw new IllegalArgumentException("extra");
                }
            } catch (IllegalArgumentException ex) {
                System.out.println("error: " + line);
                continue;
            }
            String value;
            try {
                value = Long.toString(eval(e, env));
            } catch (Unbound ex) {
                value = "unbound";
            } catch (ArithmeticException ex) {
                value = "overflow";
            }
            System.out.println(show(e) + " => " + show(simplify(e)) + " = " + value);
        }
    }
}
