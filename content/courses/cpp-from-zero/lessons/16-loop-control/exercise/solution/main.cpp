#include <iostream>

int main() {
    int price = 0;
    int sum = 0;
    int errors = 0;

    while (std::cin >> price) {
        if (price == 0) {
            break;
        }
        if (price < 0) {
            errors++;
            continue;
        }
        sum += price;
    }

    std::cout << "Сумма: " << sum << "\n";
    std::cout << "Ошибок: " << errors << "\n";
}
