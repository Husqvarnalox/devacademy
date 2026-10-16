#include <algorithm>
#include <iostream>
#include <vector>

// Максимальная сумма непустого непрерывного отрезка. Суммы выходят за int, поэтому long long.
long long best_segment(const std::vector<long long>& a) {
    // TODO: верните максимальную сумму непустого отрезка за один проход (O(n))
    (void)a;
    return 0;
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
