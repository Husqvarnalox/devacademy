#include <iostream>

int main() {
    int total = 0;
    int people = 1;
    std::cin >> total >> people;

    double share = static_cast<double>(total) / people;
    int whole = static_cast<int>(share);

    std::cout << "Каждому: " << share << "\n";
    std::cout << "Целая часть: " << whole << "\n";
}
