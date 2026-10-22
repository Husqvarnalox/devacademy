#include <iostream>

int main() {
    int apples = 47;   // яблок всего
    int perBox = 6;    // яблок в одном ящике
    int price = 35;    // цена одного яблока, руб.

    int boxes = apples / perBox;
    int left = apples % perBox;
    int total = apples * price;
    total -= 145;

    std::cout << "Полных ящиков: " << boxes << "\n";
    std::cout << "Остаток яблок: " << left << "\n";
    std::cout << "Стоимость: " << total << "\n";
}
