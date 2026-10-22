#include <iomanip>
#include <iostream>
#include <sstream>
#include <stdexcept>
#include <string>

// Строгий разбор целого: вся строка должна быть числом, результат больше нуля.
bool parsePositiveInt(const std::string& text, int& out) {
    try {
        std::size_t used = 0;
        int value = std::stoi(text, &used);
        if (used != text.size() || value <= 0) {
            return false;
        }
        out = value;
        return true;
    } catch (const std::invalid_argument&) {
        return false;
    } catch (const std::out_of_range&) {
        return false;
    }
}

// Строгий разбор дробного числа: вся строка должна быть числом, результат больше нуля.
bool parsePositiveDouble(const std::string& text, double& out) {
    try {
        std::size_t used = 0;
        double value = std::stod(text, &used);
        if (used != text.size() || !(value > 0)) {
            return false;
        }
        out = value;
        return true;
    } catch (const std::invalid_argument&) {
        return false;
    } catch (const std::out_of_range&) {
        return false;
    }
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
