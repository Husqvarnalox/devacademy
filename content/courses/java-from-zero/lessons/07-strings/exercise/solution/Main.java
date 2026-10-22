public class Main {
    public static void main(String[] args) {
        String word = "программирование";
        System.out.println("Слово: " + word);
        System.out.println("Длина: " + word.length());
        System.out.println("Первая буква: " + word.charAt(0));
        System.out.println("Последняя буква: " + word.charAt(word.length() - 1));
        System.out.println("Заглавными: " + word.toUpperCase());
    }
}
