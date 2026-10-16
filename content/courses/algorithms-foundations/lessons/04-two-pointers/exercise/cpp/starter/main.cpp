#include <iostream>
#include <vector>

// Число непустых непрерывных отрезков a с суммой не больше limit (все a[i] > 0). Суммы и ответ - long long.
long long count_segments(const std::vector<long long>& a, long long limit) {
    // TODO: два указателя (скользящее окно), O(n)
    (void)a;
    (void)limit;
    return 0;
}

int main() {
    std::ios::sync_with_stdio(false);
    std::cin.tie(nullptr);
    int n;
    long long limit;
    std::cin >> n >> limit;
    std::vector<long long> a(n);
    for (auto& x : a) std::cin >> x;
    std::cout << count_segments(a, limit) << "\n";
}
