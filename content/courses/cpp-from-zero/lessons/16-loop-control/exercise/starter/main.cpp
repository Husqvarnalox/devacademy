#include <iostream>

int main() {
    int price = 0;
    int sum = 0;
    int errors = 0;

    while (std::cin >> price) {
        // TODO: если price == 0 - break;
        // TODO: если price < 0 - посчитайте ошибку и continue;
        // Остальные значения прибавляйте к сумме.
        sum += price;
    }

    std::cout << "Сумма: " << sum << "\n";
    std::cout << "Ошибок: " << errors << "\n";
}
