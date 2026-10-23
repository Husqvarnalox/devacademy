#include <iostream>

int main() {
    int age = 0;
    int hour = 0;
    std::cin >> age >> hour;

    bool adult = age >= 18;
    bool working = hour >= 9 && hour < 18;
    bool both = adult && working;

    std::cout << std::boolalpha;
    std::cout << "Взрослый: " << adult << "\n";
    std::cout << "Рабочее время: " << working << "\n";
    std::cout << "Оба условия: " << both << "\n";
}
