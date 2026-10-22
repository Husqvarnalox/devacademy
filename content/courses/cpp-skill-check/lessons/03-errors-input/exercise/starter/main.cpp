#include <iomanip>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>

// Строгий разбор целого: вся строка должна быть числом, результат больше нуля.
bool parsePositiveInt(const std::string& text, int& out) {
    // TODO: std::stoi(text, &used) бросает исключения invalid_argument и out_of_range; used должно совпасть с text.size()
    if (text.empty()) {
        return false;
    }
    out = std::stoi(text);
    return true;
}

// Строгий разбор дробного числа: вся строка должна быть числом, результат больше нуля.
bool parsePositiveDouble(const std::string& text, double& out) {
    // TODO: то же для std::stod; условие !(value > 0) отвергает и ноль, и отрицательные числа, и nan
    if (text.empty()) {
        return false;
    }
    out = std::stod(text);
    return true;
}

int main() {
    std::string line;
    double total = 0.0;
    int bad = 0;
    while (std::getline(std::cin, line)) {
        std::istringstream words(line);
        std::string item;
        std::string quantityText;
        std::string priceText;
        std::string extra;
        int quantity = 0;
        double price = 0.0;
        if (!(words >> item >> quantityText >> priceText) || (words >> extra) || !parsePositiveInt(quantityText, quantity) ||
            !parsePositiveDouble(priceText, price)) {
            bad++;
            continue;
        }
        double sum = quantity * price;
        total += sum;
        std::cout << item << ": " << std::fixed << std::setprecision(2) << sum << "\n";
    }
    std::cout << "total=" << std::fixed << std::setprecision(2) << total << " bad=" << bad << "\n";
}
