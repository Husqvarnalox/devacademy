import java.util.Scanner;

public class Main {
    static Object parse(String kind, String value) {
        // TODO: верните Integer, Long, Double или String в зависимости от kind
        return value;
    }

    static boolean same(Object a, Object b) {
        // TODO: сравнивайте содержимое объектов, а не ссылки
        return a == b;
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
