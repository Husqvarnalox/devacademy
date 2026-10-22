import java.util.Scanner;

class Wallet {
    String owner;
    int balance;

    // TODO: конструктор Wallet(String owner, int balance)

    void add(int amount) {
        // TODO
    }

    boolean spend(int amount) {
        return false; // TODO
    }

    String describe() {
        return ""; // TODO
    }
}

public class Main {
    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        String owner = in.next();
        int start = in.nextInt();
        Wallet wallet = new Wallet(owner, start);
        int n = in.nextInt();
        for (int i = 0; i < n; i++) {
            String command = in.next();
            int amount = in.nextInt();
            if (command.equals("add")) {
                wallet.add(amount);
            } else if (command.equals("spend")) {
                if (wallet.spend(amount)) {
                    System.out.println("Покупка на " + amount + " выполнена");
                } else {
                    System.out.println("Не хватает денег на " + amount);
                }
            } else {
                System.out.println("Неизвестная команда: " + command);
            }
        }
        System.out.println(wallet.describe());
    }
}
