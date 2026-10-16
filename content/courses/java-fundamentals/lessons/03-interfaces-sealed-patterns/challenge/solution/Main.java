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

    static String show(Expr e) {
        return switch (e) {
            case Num n -> Long.toString(n.v());
            case Var v -> v.name();
            case Add(Expr l, Expr r) -> "(" + show(l) + " + " + show(r) + ")";
            case Sub(Expr l, Expr r) -> "(" + show(l) + " - " + show(r) + ")";
            case Mul(Expr l, Expr r) -> "(" + show(l) + " * " + show(r) + ")";
            case Neg(Expr x) -> "neg(" + show(x) + ")";
        };
    }

    static long eval(Expr e, Map<String, Long> env) {
        return switch (e) {
            case Num n -> n.v();
            case Var v -> {
                Long x = env.get(v.name());
                if (x == null) {
                    throw new Unbound(v.name());
                }
                yield x;
            }
            case Add(Expr l, Expr r) -> {
                long a = eval(l, env);
                long b = eval(r, env);
                yield Math.addExact(a, b);
            }
            case Sub(Expr l, Expr r) -> {
                long a = eval(l, env);
                long b = eval(r, env);
                yield Math.subtractExact(a, b);
            }
            case Mul(Expr l, Expr r) -> {
                long a = eval(l, env);
                long b = eval(r, env);
                yield Math.multiplyExact(a, b);
            }
            case Neg(Expr x) -> Math.negateExact(eval(x, env));
        };
    }

    static Expr simplify(Expr e) {
        return switch (e) {
            case Num n -> n;
            case Var v -> v;
            case Neg(Expr x) -> neg(simplify(x));
            case Add(Expr l, Expr r) -> add(simplify(l), simplify(r));
            case Sub(Expr l, Expr r) -> sub(simplify(l), simplify(r));
            case Mul(Expr l, Expr r) -> mul(simplify(l), simplify(r));
        };
    }

    static Expr neg(Expr x) {
        return switch (x) {
            case Num(long k) when k != Long.MIN_VALUE -> new Num(-k);
            case Neg(Expr inner) -> inner;
            default -> new Neg(x);
        };
    }

    static Expr add(Expr l, Expr r) {
        if (l instanceof Num(long a) && r instanceof Num(long b)) {
            try {
                return new Num(Math.addExact(a, b));
            } catch (ArithmeticException ex) {
                return new Add(l, r);
            }
        }
        if (l instanceof Num(long a) && a == 0) return r;
        if (r instanceof Num(long b) && b == 0) return l;
        return new Add(l, r);
    }

    static Expr sub(Expr l, Expr r) {
        if (l instanceof Num(long a) && r instanceof Num(long b)) {
            try {
                return new Num(Math.subtractExact(a, b));
            } catch (ArithmeticException ex) {
                return new Sub(l, r);
            }
        }
        if (r instanceof Num(long b) && b == 0) return l;
        return new Sub(l, r);
    }

    static Expr mul(Expr l, Expr r) {
        if (l instanceof Num(long a) && r instanceof Num(long b)) {
            try {
                return new Num(Math.multiplyExact(a, b));
            } catch (ArithmeticException ex) {
                return new Mul(l, r);
            }
        }
        if (l instanceof Num(long a) && a == 0) return new Num(0);
        if (r instanceof Num(long b) && b == 0) return new Num(0);
        if (l instanceof Num(long a) && a == 1) return r;
        if (r instanceof Num(long b) && b == 1) return l;
        return new Mul(l, r);
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
