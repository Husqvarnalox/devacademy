import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int m = in.nextInt();
        switch (m) {
            case 1, 3, 5, 7, 8, 10, 12 -> System.out.println("Дней: 31");
            case 4, 6, 9, 11 -> System.out.println("Дней: 30");
            case 2 -> System.out.println("Дней: 28");
            default -> System.out.println("Нет такого месяца");
        }
    }
}
