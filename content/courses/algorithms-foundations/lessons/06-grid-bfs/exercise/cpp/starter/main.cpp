#include <algorithm>
#include <iostream>
#include <string>
#include <vector>

// Наименьшее число шагов от 'S' до 'E' (4 направления), если можно войти не более чем в одну клетку '#'. Иначе -1.
int shortest_exit(const std::vector<std::string>& grid, int n, int m) {
    // TODO: поиск в ширину по состояниям (клетка, использована ли уже пробивка стены), O(n * m)
    (void)grid;
    (void)n;
    (void)m;
    return -1;
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
