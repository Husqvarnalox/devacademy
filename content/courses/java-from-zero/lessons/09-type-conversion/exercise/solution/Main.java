import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        String countText = in.next();
        String priceText = in.next();
        int count = Integer.parseInt(countText);
        double price = Double.parseDouble(priceText);
        System.out.println("Вдвое больше: " + count * 2);
        System.out.println("Половина: " + (double) count / 2);
        System.out.println("Целая половина: " + count / 2);
        System.out.println("Стоимость: " + count * price);
    }
}
