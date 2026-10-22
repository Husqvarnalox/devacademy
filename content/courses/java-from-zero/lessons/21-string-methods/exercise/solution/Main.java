import java.util.Scanner;

public class Main {
    // Часть записи с номером index без пробелов по краям
    static String field(String line, int index) {
        String[] parts = line.split(";");
        return parts[index].trim();
    }

    // Часть адреса после @ или "" если @ нет
    static String domain(String email) {
        int at = email.indexOf("@");
        if (at == -1) {
            return "";
        }
        return email.substring(at + 1);
    }

    // Совпадают ли имена без учёта регистра и крайних пробелов
    static boolean sameName(String a, String b) {
        return a.trim().toLowerCase().equals(b.trim().toLowerCase());
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        String line = in.nextLine();
        String query = in.nextLine();
        String name = field(line, 0);
        System.out.println("Имя: " + name);
        System.out.println("Город: " + field(line, 2));
        System.out.println("Домен: " + domain(field(line, 1)));
        System.out.println("Совпадает: " + sameName(name, query));
    }
}
