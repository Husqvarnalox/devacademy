#include <algorithm>
#include <iomanip>
#include <iostream>

int main() {
    int n;
    std::cin >> n;
    long long sum = 0;
    long long mn = 0;
    long long mx = 0;
    for (int i = 0; i < n; ++i) {
        long long x;
        std::cin >> x;
        sum += x;
        if (i == 0 || x < mn) mn = x;
        if (i == 0 || x > mx) mx = x;
    }
    std::cout << "sum=" << sum << "\n";
    std::cout << "min=" << mn << "\n";
    std::cout << "max=" << mx << "\n";
    std::cout << "avg=" << std::fixed << std::setprecision(2) << static_cast<double>(sum) / n << "\n";
}
