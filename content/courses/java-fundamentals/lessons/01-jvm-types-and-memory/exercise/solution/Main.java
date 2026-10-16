import java.util.Scanner;

public class Main {
    public static void main(String[] args) {
        Scanner sc = new Scanner(System.in);
        int n = sc.nextInt();
        for (int i = 0; i < n; i++) {
            long x = sc.nextLong();
            String exact;
            try {
                exact = String.valueOf(Math.toIntExact(x));
            } catch (ArithmeticException e) {
                exact = "overflow";
            }
            System.out.println(x + ": byte=" + (byte) x + " short=" + (short) x + " int=" + (int) x
                    + " char=" + (int) (char) x + " exact=" + exact);
        }
    }
}
