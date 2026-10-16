import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.Arrays;
import java.util.StringTokenizer;

public class Main {
    // Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1.
    static int shortestExit(String[] grid, int n, int m) {
        int start = -1, end = -1;
        boolean[] wall = new boolean[n * m];
        for (int r = 0; r < n; r++) {
            for (int c = 0; c < m; c++) {
                char ch = grid[r].charAt(c);
                if (ch == 'S') start = r * m + c;
                if (ch == 'E') end = r * m + c;
                wall[r * m + c] = ch == '#';
            }
        }
        // состояние = (клетка, сколько стен уже пробито: 0 или 1), кодируем числом cell * 2 + used
        int[] dist = new int[2 * n * m];
        Arrays.fill(dist, -1);
        int[] queue = new int[2 * n * m];
        int head = 0, tail = 0;
        dist[start * 2] = 0;
        queue[tail++] = start * 2;
        int[] dr = {1, -1, 0, 0};
        int[] dc = {0, 0, 1, -1};
        while (head < tail) {
            int state = queue[head++];
            int cell = state >> 1, used = state & 1;
            int d = dist[state] + 1;
            int r = cell / m, c = cell % m;
            for (int k = 0; k < 4; k++) {
                int nr = r + dr[k], nc = c + dc[k];
                if (nr < 0 || nr >= n || nc < 0 || nc >= m) continue;
                int ncell = nr * m + nc;
                int nused = used + (wall[ncell] ? 1 : 0);
                if (nused > 1) continue;
                int nstate = ncell * 2 + nused;
                if (dist[nstate] < 0) {
                    dist[nstate] = d;
                    queue[tail++] = nstate;
                }
            }
        }
        int a = dist[end * 2], b = dist[end * 2 + 1];
        if (a < 0) return b;          // b тоже может быть -1
        if (b < 0) return a;
        return Math.min(a, b);
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
