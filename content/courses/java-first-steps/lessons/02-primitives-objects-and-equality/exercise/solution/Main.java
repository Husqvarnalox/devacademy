import java.util.Objects;
import java.util.Scanner;

public class Main {
    static Object parse(String kind, String value) {
        switch (kind) {
            case "int":
                return Integer.valueOf(Integer.parseInt(value));
            case "long":
                return Long.valueOf(Long.parseLong(value));
            case "double":
                return Double.valueOf(Double.parseDouble(value));
            default:
                return value;
        }
    }

    static boolean same(Object a, Object b) {
        return Objects.equals(a, b);
    }

    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int n = sc.nextInt();
        for (int i = 0; i < n; i++) {
            Object a = parse(sc.next(), sc.next());
            Object b = parse(sc.next(), sc.next());
            System.out.println(same(a, b));
        }
    }
}
