import java.util.Scanner;

public class Main {
    // Можно ли превратить текст в целое число
    static boolean isNumber(String text) {
        return false; // TODO
    }

    // Возраст в диапазоне 0..150 или исключение
    static int checkedAge(int age) {
        return age; // TODO
    }

    // Число из текста или fallback
    static int parseOrDefault(String text, int fallback) {
        return fallback; // TODO
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int n = in.nextInt();
        int ok = 0;
        int bad = 0;
        for (int i = 0; i < n; i++) {
            String token = in.next();
            if (!isNumber(token)) {
                System.out.println("Не число: " + token);
                bad++;
                continue;
            }
            try {
                int age = checkedAge(Integer.parseInt(token));
                System.out.println("Возраст: " + age);
                ok++;
            } catch (IllegalArgumentException e) {
                System.out.println("Ошибка: " + e.getMessage());
                bad++;
            }
        }
        System.out.println("Принято: " + ok + ", отклонено: " + bad);
        String limit = in.next();
        System.out.println("Лимит: " + parseOrDefault(limit, 100));
    }
}
