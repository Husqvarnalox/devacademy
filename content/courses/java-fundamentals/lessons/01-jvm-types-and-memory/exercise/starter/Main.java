import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int n = sc.nextInt();
        for (int i = 0; i < n; i++) {
            long x = sc.nextLong();
            // TODO: сузьте x до byte, short, int и char (как число) приведением типа,
            // а для exact используйте Math.toIntExact и перехватите ArithmeticException
            System.out.println(x + ": byte=" + x + " short=" + x + " int=" + x + " char=" + x + " exact=" + x);
        }
    }
}
