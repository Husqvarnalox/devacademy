import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int count = 0;
        int sum = 0;
        int even = 0;
        int max = 0;
        while (in.hasNextInt()) {
            int x = in.nextInt();
            if (count == 0 || x > max) {
                max = x;
            }
            sum += x;
            count++;
            if (x % 2 == 0) {
                even++;
            }
        }
        int average = 0;
        if (count > 0) {
            average = sum / count;
        }
        System.out.println("Чисел: " + count);
        System.out.println("Сумма: " + sum);
        System.out.println("Чётных: " + even);
        System.out.println("Среднее: " + average);
        System.out.println("Максимум: " + max);
    }
}
