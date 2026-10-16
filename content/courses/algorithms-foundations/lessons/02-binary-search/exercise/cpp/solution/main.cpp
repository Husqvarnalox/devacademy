#include <algorithm>
#include <iostream>
#include <vector>

// Сколько кусков длины length получится из всех досок (сумма выходит за int, поэтому long long).
long long pieces(const std::vector<long long>& a, long long length) {
    long long total = 0;
    for (long long x : a) total += x / length;
    return total;
}

// Наибольшая целая длина L >= 1, при которой выходит не менее k кусков; 0, если такой L нет.
long long longest_piece(const std::vector<long long>& a, long long k) {
    long long lo = 0, hi = *std::max_element(a.begin(), a.end()) + 1;  // lo подходит (0 - условно), hi не подходит
    while (hi - lo > 1) {
        long long mid = lo + (hi - lo) / 2;
        if (pieces(a, mid) >= k) lo = mid;
        else hi = mid;
    }
    return lo;
}

int main() {
    std::ios::sync_with_stdio(false);
    std::cin.tie(nullptr);
    int n;
    long long k;
    std::cin >> n >> k;
    std::vector<long long> a(n);
    for (auto& x : a) std::cin >> x;
    std::cout << longest_piece(a, k) << "\n";
}
