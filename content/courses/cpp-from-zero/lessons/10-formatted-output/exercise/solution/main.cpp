#include <iomanip>
#include <iostream>

int main() {
    double price = 0.0;
    int quantity = 0;
    std::cin >> price >> quantity;

    double total = price * quantity;

    std::cout << std::fixed << std::setprecision(2);

    std::cout << "Цена: " << price << "\n";
    std::cout << "Количество: " << quantity << "\n";
    std::cout << "Итого: " << std::setw(10) << total << "\n";
}
