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
    // TODO: бинарный поиск по ответу - длине куска (O(n log max(a)))
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
    std::cout << longest_piece(a, k) << "\n";
}
