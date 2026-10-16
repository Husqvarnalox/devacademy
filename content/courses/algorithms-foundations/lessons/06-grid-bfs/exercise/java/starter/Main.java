import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.Arrays;
import java.util.StringTokenizer;

public class Main {
    // Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1.
    static int shortestExit(String[] grid, int n, int m) {
        // TODO: поиск в ширину по состояниям (клетка, использована ли уже пробивка стены), O(n * m)
        return -1;
    }

    public static void main(String[] args) throws IOException {
        BufferedReader br = new BufferedReader(new InputStreamReader(System.in));
        StringTokenizer st = new StringTokenizer(br.readLine());
        int n = Integer.parseInt(st.nextToken());
        int m = Integer.parseInt(st.nextToken());
        String[] grid = new String[n];
        for (int i = 0; i < n; i++) grid[i] = br.readLine().trim();
        System.out.println(shortestExit(grid, n, m));
    }
}
