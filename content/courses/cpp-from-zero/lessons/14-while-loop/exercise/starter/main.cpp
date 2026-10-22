#include <iostream>

int main() {
    long long sum = 0;
    long long best = 0;
    int count = 0;

    // TODO: объявите переменную для цены и читайте цены до конца ввода: while (std::cin >> price) { ... }
    // В цикле считайте количество, сумму и наибольшую цену.

    if (count == 0) {
        std::cout << "Чек пуст\n";
    } else {
        std::cout << "Товаров: " << count << "\n";
        std::cout << "Сумма: " << sum << "\n";
        std::cout << "Самый дорогой: " << best << "\n";
    }
}
