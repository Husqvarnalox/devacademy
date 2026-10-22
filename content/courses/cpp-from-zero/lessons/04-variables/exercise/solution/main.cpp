#include <iostream>
#include <string>

int main() {
    std::string product = "Хлеб";
    int count = 3;
    int price = 60;
    count = 5;

    std::cout << "Товар: " << product << "\n";
    std::cout << "Количество: " << count << "\n";
    std::cout << "Цена за штуку: " << price << " руб.\n";
}
