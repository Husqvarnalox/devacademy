#include <iostream>
#include <vector>

// Число непустых непрерывных отрезков a с суммой не больше limit (все a[i] > 0). Суммы и ответ - long long.
long long count_segments(const std::vector<long long>& a, long long limit) {
    long long total = 0, cur = 0;
    size_t left = 0;  // окно a[left..right]; инвариант: его сумма cur <= limit (или окно пусто)
    for (size_t right = 0; right < a.size(); right++) {
        cur += a[right];
        while (cur > limit) {
            cur -= a[left];
            left++;
        }
        total += static_cast<long long>(right - left + 1);  // подходящие отрезки, оканчивающиеся в right
    }
    return total;
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
