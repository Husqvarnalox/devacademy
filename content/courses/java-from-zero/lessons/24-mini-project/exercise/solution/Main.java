import java.util.ArrayList;
import java.util.Locale;
import java.util.Scanner;

class Student {
    String name;
    int score;

    Student(String name, int score) {
        this.name = name;
        this.score = score;
    }
}

public class Main {
    // Разбирает строку имя;балл или бросает IllegalArgumentException
    static Student parse(String line) {
        String[] parts = line.split(";");
        if (parts.length != 2) {
            throw new IllegalArgumentException("нужны два поля");
        }
        String name = parts[0].trim();
        if (name.isEmpty()) {
            throw new IllegalArgumentException("пустое имя");
        }
        int score;
        try {
            score = Integer.parseInt(parts[1].trim());
        } catch (NumberFormatException e) {
            throw new IllegalArgumentException("балл не число");
        }
        if (score < 0 || score > 100) {
            throw new IllegalArgumentException("балл вне диапазона");
        }
        return new Student(name, score);
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        ArrayList<Student> students = new ArrayList<>();
        int bad = 0;
        while (in.hasNextLine()) {
            String line = in.nextLine();
            if (line.trim().isEmpty()) {
                continue;
            }
            try {
                students.add(parse(line));
            } catch (IllegalArgumentException e) {
                bad++;
            }
        }

        System.out.println("Студентов: " + students.size());
        if (students.isEmpty()) {
            System.out.println("Средний балл: нет данных");
            System.out.println("Лучший: нет данных");
            System.out.println("Отличников (90 и выше): 0");
        } else {
            int sum = 0;
            int excellent = 0;
            Student best = students.get(0);
            for (Student s : students) {
                sum += s.score;
                if (s.score >= 90) {
                    excellent++;
                }
                if (s.score > best.score) {
                    best = s;
                }
            }
            double average = (double) sum / students.size();
            System.out.printf(Locale.ROOT, "Средний балл: %.2f%n", average);
            System.out.println("Лучший: " + best.name + " (" + best.score + ")");
            System.out.println("Отличников (90 и выше): " + excellent);
        }
        System.out.println("Пропущено строк: " + bad);
    }
}
