#include <iostream>

int main() {
    long long price = 0;
    long long sum = 0;
    long long best = 0;
    int count = 0;

    while (std::cin >> price) {
        count++;
        sum += price;
        if (count == 1 || price > best) {
            best = price;
        }
    }

    if (count == 0) {
        std::cout << "Чек пуст\n";
    } else {
        std::cout << "Товаров: " << count << "\n";
        std::cout << "Сумма: " << sum << "\n";
        std::cout << "Самый дорогой: " << best << "\n";
    }
}
