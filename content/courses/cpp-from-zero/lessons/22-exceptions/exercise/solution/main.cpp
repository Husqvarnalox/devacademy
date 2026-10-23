#include <iostream>
#include <stdexcept>
#include <string>

int parseQuantity(const std::string& text) {
    int value = std::stoi(text);
    if (std::to_string(value) != text) {
        throw std::invalid_argument("лишние символы в числе");
    }
    if (value <= 0) {
        throw std::invalid_argument("количество должно быть положительным");
    }
    return value;
}

int main() {
    int good = 0;
    int bad = 0;
    long long total = 0;

    std::string line = "";
    while (std::getline(std::cin, line)) {
        if (line.empty()) {
            continue;
        }
        try {
            int quantity = parseQuantity(line);
            total += quantity;
            good++;
        } catch (const std::exception&) {
            std::cout << "Ошибка в строке: " << line << "\n";
            bad++;
        }
    }

    std::cout << "Принято: " << good << "\n";
    std::cout << "Ошибок: " << bad << "\n";
    std::cout << "Сумма: " << total << "\n";
}
