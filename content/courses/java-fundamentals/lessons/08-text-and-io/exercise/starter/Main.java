import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;

public class Main {
    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in, StandardCharsets.UTF_8));
        int n = Integer.parseInt(in.readLine().trim());
        int lines = 0;
        for (int no = 1; no <= n; no++) {
            String line = in.readLine();
            if (line == null) {
                break;
            }
            String text = line.strip();
            // TODO: пропустите пустые строки и комментарии (# и ;), разберите секции [имя]
            // и пары ключ = значение, соберите их в TreeMap<String, TreeMap<String, String>>.
            // Некорректные строки сохраните как предупреждения "invalid line <номер>: <текст>".
            lines++;
        }
        System.out.println("sections=0 keys=0");
    }
}
