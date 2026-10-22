import java.util.Locale;
import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        String title = in.next();
        double price = Double.parseDouble(in.next());
        int count = in.nextInt();
        double total = price * count;
        double discounted = total * 0.9;
        System.out.printf(Locale.ROOT, "Товар: %s%n", title);
        System.out.printf(Locale.ROOT, "Цена: %.2f руб.%n", price);
        System.out.printf(Locale.ROOT, "Количество: %d%n", count);
        System.out.printf(Locale.ROOT, "Сумма: %.2f руб.%n", total);
        System.out.printf(Locale.ROOT, "Со скидкой 10%%: %.2f руб.%n", discounted);
    }
}
