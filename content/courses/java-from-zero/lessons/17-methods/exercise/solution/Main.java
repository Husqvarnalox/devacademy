import java.util.Scanner;

public class Main {
    // Цена после скидки: price - price * percent / 100 (деление целое)
    static int priceWithDiscount(int price, int percent) {
        return price - price * percent / 100;
    }

    // true, если сумма не меньше 3000
    static boolean freeDelivery(int amount) {
        return amount >= 3000;
    }

    // Печатает строку "<label>: <value>"
    static void printLine(String label, int value) {
        System.out.println(label + ": " + value);
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int price = in.nextInt();
        int percent = in.nextInt();
        int total = priceWithDiscount(price, percent);
        printLine("Цена", price);
        printLine("К оплате", total);
        System.out.println("Бесплатная доставка: " + freeDelivery(total));
    }
}
