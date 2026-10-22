import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int age = in.nextInt();
        if (age < 0) {
            System.out.println("Некорректный возраст");
        } else if (age <= 5) {
            System.out.println("Цена: 0 руб.");
        } else if (age <= 17) {
            System.out.println("Цена: 150 руб.");
        } else if (age <= 64) {
            System.out.println("Цена: 300 руб.");
        } else {
            System.out.println("Цена: 150 руб.");
        }
    }
}
