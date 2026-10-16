#include <iostream>
#include <unordered_map>
#include <vector>

// Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными). Суммы и ответ - long long.
long long count_segments(const std::vector<long long>& a, long long k) {
    std::unordered_map<long long, long long> seen;  // префиксная сумма -> сколько раз встречалась
    seen.reserve(a.size() * 2 + 2);
    seen[0] = 1;                                    // пустой префикс
    long long cur = 0, ans = 0;
    for (long long x : a) {
        cur += x;
        auto it = seen.find(cur - k);               // левые границы, для которых сумма отрезка ровно k
        if (it != seen.end()) ans += it->second;
        seen[cur]++;
    }
    return ans;
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
