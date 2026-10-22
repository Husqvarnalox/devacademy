#include <iostream>

int main() {
    int total = 0;
    int people = 1;
    std::cin >> total >> people;

    double share = total / people;  // TODO: сделайте деление дробным с помощью static_cast<double>
    int whole = 0;                  // TODO: целая часть доли: static_cast<int>(share)

    std::cout << "Каждому: " << share << "\n";
    std::cout << "Целая часть: " << whole << "\n";
}
