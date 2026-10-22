#include <iostream>

int main() {
    int age = 0;
    int hour = 0;
    std::cin >> age >> hour;

    bool adult = false;    // TODO: возраст не меньше 18
    bool working = false;  // TODO: час не меньше 9 и меньше 18
    bool both = false;     // TODO: одновременно взрослый и рабочее время

    std::cout << std::boolalpha;
    std::cout << "Взрослый: " << adult << "\n";
    std::cout << "Рабочее время: " << working << "\n";
    std::cout << "Оба условия: " << both << "\n";
}
