#include <iostream>

// Возвращает true, если год високосный.
bool isLeapYear(int year) {
    // TODO: делится на 4 и (не делится на 100 или делится на 400)
    return year % 4 == 0;
}

// Возвращает наибольшее из трёх чисел.
int maxOfThree(int a, int b, int c) {
    // TODO: найдите наибольшее и верните его
    return a + b + c;
}

// Печатает n звёздочек подряд и переводит строку.
void printStars(int n) {
    // TODO: цикл for, в каждом обороте печатайте "*"
    std::cout << n << "\n";
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
