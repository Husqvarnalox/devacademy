#include <iomanip>
#include <iostream>

int main() {
    long long steps = 0;
    int days = 0;
    // TODO: заведите переменные для суммы, лучшего значения и числа дней с целью 10000 шагов и более
    while (std::cin >> steps) {
        days++;
        // TODO: накопите сумму (тип long long), обновите максимум и посчитайте дни с целью
    }
    // TODO: среднее = сумма / число дней как double; для нуля дней - 0.00
    double average = 0.0;
    std::cout << "days=" << days << "\n";
    std::cout << "total=" << 0 << "\n";
    std::cout << "best=" << 0 << "\n";
    std::cout << "avg=" << std::fixed << std::setprecision(2) << average << "\n";
    std::cout << "goal=" << 0 << "\n";
}
