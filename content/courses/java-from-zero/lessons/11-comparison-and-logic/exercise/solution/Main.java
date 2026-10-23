import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int n = in.nextInt();
        System.out.println("Чётное: " + (n % 2 == 0));
        System.out.println("Двузначное: " + (n >= 10 && n <= 99));
        System.out.println("Делится на 3 или на 5: " + (n % 3 == 0 || n % 5 == 0));
        System.out.println("Не положительное: " + !(n > 0));
    }
}
