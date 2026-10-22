#include <iomanip>
#include <iostream>
#include <limits>
#include <string>

// Среднее двух чисел, дробная часть отбрасывается к нулю. Сумма не должна переполнять int.
int average(int a, int b) {
    // TODO: сумма двух больших int не помещается в int - считайте в long long
    return (a + b) / 2;
}

// Остаток от деления a на b (b > 0) в диапазоне от 0 до b - 1.
int floorMod(int a, int b) {
    // TODO: у отрицательного a результат % отрицателен - приведите его к диапазону 0..b-1
    return a % b;
}

// Цена в копейках после скидки в процентах; половина копейки округляется вверх.
long long discounted(long long cents, int percent) {
    // TODO: cents * (100 - percent) / 100 с округлением половины копейки вверх
    return cents * (100 - percent) / 100;
}

// Оценка по баллу: A от 90, B от 75, C от 60, иначе D; вне диапазона 0..100 - '?'.
char grade(int score) {
    // TODO: сначала отсеките значения вне 0..100, затем проверяйте границы от большей к меньшей
    if (score >= 90) {
        return 'A';
    }
    return 'D';
}

int main() {
    std::string command;
    while (std::cin >> command) {
        if (command == "avg") {
            int a = 0;
            int b = 0;
            std::cin >> a >> b;
            std::cout << average(a, b) << "\n";
        } else if (command == "mod") {
            int a = 0;
            int b = 1;
            std::cin >> a >> b;
            std::cout << floorMod(a, b) << "\n";
        } else if (command == "total") {
            long long cents = 0;
            int percent = 0;
            std::cin >> cents >> percent;
            long long result = discounted(cents, percent);
            std::cout << result / 100 << "." << std::setfill('0') << std::setw(2) << result % 100 << "\n";
        } else if (command == "grade") {
            int score = 0;
            std::cin >> score;
            std::cout << grade(score) << "\n";
        }
    }
}
