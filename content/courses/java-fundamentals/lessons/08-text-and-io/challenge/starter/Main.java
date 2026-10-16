import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

public class Main {
    // TODO: split(",") не понимает кавычки. Напишите разбор по символам: поле в кавычках может
    // содержать запятые, а "" внутри него означает одну кавычку. Ошибки выбрасывайте как
    // IllegalArgumentException("unterminated quote") и IllegalArgumentException("bad quote").
    static List<String> parse(String line) {
        return new ArrayList<>(List.of(line.split(",", -1)));
    }

    // TODO: ширину измеряйте в кодовых точках (codePointCount), а не в length().
    static int width(String s) {
        return s.length();
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in, StandardCharsets.UTF_8));
        int n = Integer.parseInt(in.readLine().trim());
        List<List<String>> rows = new ArrayList<>();
        for (int no = 1; no <= n; no++) {
            String line = in.readLine();
            if (line == null) {
                break;
            }
            rows.add(parse(line));
        }
        for (List<String> r : rows) {
            System.out.println(String.join(" | ", r));
        }
    }
}
