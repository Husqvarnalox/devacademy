import java.util.HashMap;
import java.util.Scanner;

public class Main {
    // Добавляет qty к остатку товара item
    static void addStock(HashMap<String, Integer> stock, String item, int qty) {
        // TODO
    }

    // Остаток товара или 0, если товара нет
    static int quantity(HashMap<String, Integer> stock, String item) {
        return 0; // TODO
    }

    // Сумма остатков по всем товарам
    static int totalUnits(HashMap<String, Integer> stock) {
        return 0; // TODO
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        HashMap<String, Integer> stock = new HashMap<>();
        int n = in.nextInt();
        for (int i = 0; i < n; i++) {
            String item = in.next();
            int qty = in.nextInt();
            addStock(stock, item, qty);
        }
        int q = in.nextInt();
        for (int i = 0; i < q; i++) {
            String item = in.next();
            System.out.println(item + ": " + quantity(stock, item));
        }
        System.out.println("Всего единиц: " + totalUnits(stock));
        System.out.println("Видов товара: " + stock.size());
    }
}
