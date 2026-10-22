import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int s = in.nextInt();
        int m = in.nextInt();
        int total = 0;
        for (int k = 1; k <= m; k++) {
            total += s;
            System.out.println("Месяц " + k + ": " + total);
        }
        System.out.println("Итого: " + total);
    }
}
