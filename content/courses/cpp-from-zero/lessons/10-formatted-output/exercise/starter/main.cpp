#include <iomanip>
#include <iostream>

int main() {
    double price = 0.0;
    int quantity = 0;
    std::cin >> price >> quantity;

    double total = price * quantity;

    // TODO: включите вывод с двумя знаками после точки: std::fixed и std::setprecision(2)

    std::cout << "Цена: " << price << "\n";
    std::cout << "Количество: " << quantity << "\n";
    // TODO: итог выведите в поле шириной 10 символов: std::setw(10)
    std::cout << "Итого: " << total << "\n";
}
