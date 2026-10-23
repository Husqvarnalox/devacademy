import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int count = 0;
        int sum = 0;
        int max = 0;
        while (in.hasNextInt()) {
            int price = in.nextInt();
            count++;
            sum += price;
            if (price > max) {
                max = price;
            }
        }
        System.out.println("Товаров: " + count);
        System.out.println("Сумма: " + sum);
        System.out.println("Самый дорогой: " + max);
    }
}
