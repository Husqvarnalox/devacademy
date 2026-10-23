#include <iostream>
#include <string>

int main() {
    std::string name = "";
    int price = 0;
    int quantity = 0;

    // TODO: прочитайте из ввода имя, цену и количество командой std::cin >> ...
    // На вход приходят три значения через пробел или перевод строки, например: Anna 45 3

    int total = 0;  // TODO: цена, умноженная на количество

    std::cout << "Покупатель: " << name << "\n";
    std::cout << "Сумма: " << total << "\n";
}
