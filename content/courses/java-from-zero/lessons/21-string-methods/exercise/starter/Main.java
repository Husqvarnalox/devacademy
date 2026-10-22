import java.util.Scanner;

public class Main {
    // Часть записи с номером index без пробелов по краям
    static String field(String line, int index) {
        return ""; // TODO
    }

    // Часть адреса после @ или "" если @ нет
    static String domain(String email) {
        return ""; // TODO
    }

    // Совпадают ли имена без учёта регистра и крайних пробелов
    static boolean sameName(String a, String b) {
        return false; // TODO
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
