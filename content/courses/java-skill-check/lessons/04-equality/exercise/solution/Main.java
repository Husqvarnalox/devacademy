import java.util.*;

public class Main {
    // true, если строки равны по содержимому (две null тоже равны)
    static boolean sameWord(String a, String b) {
        return Objects.equals(a, b);
    }

    // true, если числа равны по значению (две null тоже равны)
    static boolean sameNumber(Integer a, Integer b) {
        return Objects.equals(a, b);
    }

    // Сколько элементов списка равны target по значению
    static int countEqual(List<Integer> xs, Integer target) {
        int count = 0;
        for (Integer x : xs) {
            if (Objects.equals(x, target)) {
                count++;
            }
        }
        return count;
    }

    public static void main(String[] args) {
        Scanner in = new Scanner(System.in);
        while (in.hasNextLine()) {
            String line = in.nextLine().trim();
            if (line.isEmpty()) {
                continue;
            }
            String[] p = line.split(" ");
            String a = p[0].equals("null") ? null : new String(p[0]);
            String b = p[1].equals("null") ? null : new String(p[1]);
            List<Integer> nums = new ArrayList<>();
            for (int i = 2; i < p.length; i++) {
                nums.add(p[i].equals("null") ? null : Integer.valueOf(Integer.parseInt(p[i])));
            }
            System.out.println("Строки: " + sameWord(a, b) + ", числа: " + sameNumber(nums.get(0), nums.get(1))
                    + ", совпадений: " + countEqual(nums, nums.get(0)));
        }
    }
}
