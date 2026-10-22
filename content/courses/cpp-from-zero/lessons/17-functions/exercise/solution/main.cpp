#include <iostream>

// Возвращает true, если год високосный.
bool isLeapYear(int year) {
    return year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
}

// Возвращает наибольшее из трёх чисел.
int maxOfThree(int a, int b, int c) {
    int best = a;
    if (b > best) {
        best = b;
    }
    if (c > best) {
        best = c;
    }
    return best;
}

// Печатает n звёздочек подряд и переводит строку.
void printStars(int n) {
    for (int i = 0; i < n; i++) {
        std::cout << "*";
    }
    std::cout << "\n";
}

int main() {
    int year = 0;
    int a = 0;
    int b = 0;
    int c = 0;
    int n = 0;
    std::cin >> year >> a >> b >> c >> n;

    if (isLeapYear(year)) {
        std::cout << "Високосный\n";
    } else {
        std::cout << "Обычный\n";
    }
    std::cout << "Наибольшее: " << maxOfThree(a, b, c) << "\n";
    printStars(n);
}
