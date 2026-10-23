import java.util.Scanner;

public class Main {
    // Целое положительное число из строки; иначе IllegalArgumentException("Некорректная сумма: " + text)
    static int parseAmount(String text) {
        return 0; // TODO
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        Account acc = new Account(in.nextLine().trim());
        int errors = 0;
        while (in.hasNextLine()) {
            String line = in.nextLine().trim();
            if (line.isEmpty()) {
                continue;
            }
            String[] parts = line.split("\\s+");
            try {
                if (parts.length != 2) {
                    throw new IllegalArgumentException("Неверная команда: " + line);
                }
                int amount = parseAmount(parts[1]);
                switch (parts[0]) {
                    case "deposit" -> acc.deposit(amount);
                    case "withdraw" -> acc.withdraw(amount);
                    default -> throw new IllegalArgumentException("Неизвестная команда: " + parts[0]);
                }
            } catch (IllegalArgumentException | IllegalStateException e) {
                errors++;
                System.out.println("Ошибка: " + e.getMessage());
            }
        }
        System.out.println(acc);
        System.out.println("Ошибок: " + errors);
    }
}

class Account {
    // TODO: поля owner (final) и balance, конструктор Account(String owner)

    Account(String owner) {
    }

    void deposit(int amount) {
        // TODO
    }

    // Недостаток средств: IllegalStateException("Недостаточно средств: нужно N, есть M")
    void withdraw(int amount) {
        // TODO
    }

    int getBalance() {
        return 0; // TODO
    }

    @Override
    public String toString() {
        return ""; // TODO: "<владелец>: <баланс>"
    }
}
