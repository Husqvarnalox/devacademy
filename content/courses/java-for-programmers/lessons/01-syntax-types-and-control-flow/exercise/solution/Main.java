import java.util.Scanner;

public class Main {
    // Стоимость priceCents * qty как long (без переполнения int)
    static long totalCents(int priceCents, int qty) {
        return (long) priceCents * qty;
    }

    // Доля part от total в процентах, округление Math.round
    static int percent(int part, int total) {
        return (int) Math.round(part * 100.0 / total);
    }

    // Сдвиг строчных латинских букв по кругу на shift позиций
    static String caesar(String word, int shift) {
        String result = "";
        for (int i = 0; i < word.length(); i++) {
            char c = word.charAt(i);
            if (c >= 'a' && c <= 'z') {
                c = (char) ('a' + Math.floorMod(c - 'a' + shift, 26));
            }
            result += c;
        }
        return result;
    }

    // "weekend" для sat и sun, "weekday" для mon-fri, иначе "unknown"
    static String dayType(String day) {
        return switch (day) {
            case "sat", "sun" -> "weekend";
            case "mon", "tue", "wed", "thu", "fri" -> "weekday";
            default -> "unknown";
        };
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int price = in.nextInt();
        int qty = in.nextInt();
        int part = in.nextInt();
        int total = in.nextInt();
        String word = in.next();
        int shift = in.nextInt();
        String day = in.next();
        System.out.println("Сумма: " + totalCents(price, qty));
        System.out.println("Доля: " + percent(part, total) + "%");
        System.out.println("Шифр: " + caesar(word, shift));
        System.out.println("День: " + dayType(day));
    }
}
