#include <iomanip>
#include <iostream>

int main() {
    long long steps = 0;
    int days = 0;
    int goalDays = 0;
    long long total = 0;
    long long best = 0;
    while (std::cin >> steps) {
        days++;
        total += steps;
        if (steps > best) {
            best = steps;
        }
        if (steps >= 10000) {
            goalDays++;
        }
    }
    double average = 0.0;
    if (days > 0) {
        average = static_cast<double>(total) / days;
    }
    std::cout << "days=" << days << "\n";
    std::cout << "total=" << total << "\n";
    std::cout << "best=" << best << "\n";
    std::cout << "avg=" << std::fixed << std::setprecision(2) << average << "\n";
    std::cout << "goal=" << goalDays << "\n";
}
