#include <iostream>
#include <string>

int main() {
    std::string name = "";
    int price = 0;
    int quantity = 0;

    std::cin >> name >> price >> quantity;

    int total = price * quantity;

    std::cout << "Покупатель: " << name << "\n";
    std::cout << "Сумма: " << total << "\n";
}
