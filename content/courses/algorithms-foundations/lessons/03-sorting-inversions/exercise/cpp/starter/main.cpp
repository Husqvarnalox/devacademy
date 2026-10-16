#include <iostream>
#include <vector>

// Сортирует a[lo, hi) на месте (tmp - буфер) и возвращает число пар i < j с a[i] > a[j]. Ответ до ~5e9 - long long.
long long sort_count(std::vector<int>& a, std::vector<int>& tmp, int lo, int hi) {
    // TODO: сортировка слиянием, которая заодно считает инверсии (O(n log n))
    (void)a;
    (void)tmp;
    return 0;
}

int main() {
    std::ios::sync_with_stdio(false);
    std::cin.tie(nullptr);
    int n;
    std::cin >> n;
    std::vector<int> a(n), tmp(n);
    for (auto& x : a) std::cin >> x;
    std::cout << sort_count(a, tmp, 0, n) << "\n";
}
