#include <iostream>
#include <vector>

// Сортирует a[lo, hi) на месте (tmp - буфер) и возвращает число пар i < j с a[i] > a[j]. Ответ до ~5e9 - long long.
long long sort_count(std::vector<int>& a, std::vector<int>& tmp, int lo, int hi) {
    if (hi - lo <= 1) return 0;
    int mid = lo + (hi - lo) / 2;
    long long inv = sort_count(a, tmp, lo, mid) + sort_count(a, tmp, mid, hi);
    int i = lo, j = mid, k = lo;
    while (i < mid && j < hi) {
        if (a[i] <= a[j]) {
            tmp[k++] = a[i++];
        } else {
            tmp[k++] = a[j++];
            inv += mid - i;  // a[j] меньше всех оставшихся слева
        }
    }
    while (i < mid) tmp[k++] = a[i++];
    while (j < hi) tmp[k++] = a[j++];
    for (int t = lo; t < hi; t++) a[t] = tmp[t];
    return inv;
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
