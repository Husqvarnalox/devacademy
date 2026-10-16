#include <iostream>
#include <unordered_map>
#include <vector>

// Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными). Суммы и ответ - long long.
long long count_segments(const std::vector<long long>& a, long long k) {
    // TODO: префиксные суммы + unordered_map "сумма -> сколько раз встречалась", O(n)
    (void)a;
    (void)k;
    return 0;
}

int main() {
    std::ios::sync_with_stdio(false);
    std::cin.tie(nullptr);
    int n;
    long long k;
    std::cin >> n >> k;
    std::vector<long long> a(n);
    for (auto& x : a) std::cin >> x;
    std::cout << count_segments(a, k) << "\n";
}
