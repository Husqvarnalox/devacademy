import java.util.ArrayList;
import java.util.Scanner;

public class Main {
    // Новый список из оценок не ниже min, в исходном порядке
    static ArrayList<Integer> passing(ArrayList<Integer> grades, int min) {
        return new ArrayList<>(); // TODO
    }

    // Наибольшая оценка (список не пуст)
    static int best(ArrayList<Integer> grades) {
        return 0; // TODO
    }

    // Есть ли в списке оценка 5
    static boolean hasFive(ArrayList<Integer> grades) {
        return false; // TODO
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        int min = in.nextInt();
        ArrayList<Integer> grades = new ArrayList<>();
        while (in.hasNextInt()) {
            grades.add(in.nextInt());
        }
        System.out.println("Всего оценок: " + grades.size());
        System.out.println("Не ниже " + min + ": " + passing(grades, min));
        System.out.println("Лучшая: " + best(grades));
        System.out.println("Пятёрка есть: " + hasFive(grades));
    }
}
