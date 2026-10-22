#include <iostream>
#include <stdexcept>
#include <string>

int parseQuantity(const std::string& text) {
    // TODO: проверьте, что строка целиком является положительным целым числом,
    // иначе бросьте std::invalid_argument
    return std::stoi(text);
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
