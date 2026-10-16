#include <algorithm>
#include <iostream>
#include <string>
#include <vector>

// Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1.
int shortest_exit(const std::vector<std::string>& grid, int n, int m) {
    int start = -1, end = -1;
    for (int r = 0; r < n; r++)
        for (int c = 0; c < m; c++) {
            if (grid[r][c] == 'S') start = r * m + c;
            if (grid[r][c] == 'E') end = r * m + c;
        }
    // состояние = (клетка, сколько стен уже пробито: 0 или 1), кодируем числом cell * 2 + used
    std::vector<int> dist(2 * n * m, -1);
    std::vector<int> queue;
    queue.reserve(2 * n * m);
    dist[start * 2] = 0;
    queue.push_back(start * 2);
    const int dr[4] = {1, -1, 0, 0};
    const int dc[4] = {0, 0, 1, -1};
    for (size_t head = 0; head < queue.size(); head++) {
        int state = queue[head];
        int cell = state >> 1, used = state & 1;
        int d = dist[state] + 1;
        int r = cell / m, c = cell % m;
        for (int k = 0; k < 4; k++) {
            int nr = r + dr[k], nc = c + dc[k];
            if (nr < 0 || nr >= n || nc < 0 || nc >= m) continue;
            int nused = used + (grid[nr][nc] == '#' ? 1 : 0);
            if (nused > 1) continue;
            int nstate = (nr * m + nc) * 2 + nused;
            if (dist[nstate] < 0) {
                dist[nstate] = d;
                queue.push_back(nstate);
            }
        }
    }
    int a = dist[end * 2], b = dist[end * 2 + 1];
    if (a < 0) return b;  // b тоже может быть -1
    if (b < 0) return a;
    return std::min(a, b);
}

int main() {
    std::ios::sync_with_stdio(false);
    std::cin.tie(nullptr);
    int n, m;
    std::cin >> n >> m;
    std::vector<std::string> grid(n);
    for (auto& row : grid) std::cin >> row;
    std::cout << shortest_exit(grid, n, m) << "\n";
}
