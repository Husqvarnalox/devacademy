#include <algorithm>
#include <iostream>
#include <vector>

// Максимальная сумма непустого непрерывного отрезка. Суммы выходят за int, поэтому long long.
long long best_segment(const std::vector<long long>& a) {
    long long best = a[0], cur = a[0];
    for (size_t i = 1; i < a.size(); i++) {
        cur = std::max(a[i], cur + a[i]);
        best = std::max(best, cur);
    }
    return best;
}

int main() {
    std::ios::sync_with_stdio(false);
    std::cin.tie(nullptr);
    int n;
    std::cin >> n;
    std::vector<long long> a(n);
    for (auto& x : a) std::cin >> x;
    std::cout << best_segment(a) << "\n";
}
