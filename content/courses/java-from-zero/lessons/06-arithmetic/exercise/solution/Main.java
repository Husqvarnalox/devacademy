public class Main {
    public static void main(String[] args) {
        int totalMinutes = 135;
        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;
        System.out.println("Всего минут: " + totalMinutes);
        System.out.println("Часов: " + hours);
        System.out.println("Остаток минут: " + minutes);
    }
}
