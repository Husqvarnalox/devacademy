#include <charconv>
#include <climits>
#include <iostream>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <system_error>
#include <vector>

// Строгий разбор: вся строка должна быть целым числом в диапазоне int (допустим ведущий минус).
std::optional<int> parseStrict(const std::string& text) {
    int value = 0;
    const char* first = text.data();
    const char* last = text.data() + text.size();
    std::from_chars_result result = std::from_chars(first, last, value);
    if (result.ec != std::errc() || result.ptr != last) {
        return std::nullopt;
    }
    return value;
}

// Сумма двух int; std::nullopt, если результат не помещается в int. Без неопределённого поведения.
std::optional<int> checkedAdd(int a, int b) {
    if (b > 0 && a > INT_MAX - b) {
        return std::nullopt;
    }
    if (b < 0 && a < INT_MIN - b) {
        return std::nullopt;
    }
    return a + b;
}

// Деление нацело. Бросает std::invalid_argument при делении на ноль
// и std::overflow_error, если результат не помещается в int (INT_MIN / -1).
int divide(int a, int b) {
    if (b == 0) {
        throw std::invalid_argument("division by zero");
    }
    if (a == INT_MIN && b == -1) {
        throw std::overflow_error("result does not fit in int");
    }
    return a / b;
}

// Элемент по индексу с проверкой границ: при выходе за границы бросает std::out_of_range.
int elementAt(const std::vector<int>& v, std::size_t index) {
    return v.at(index);
}

// Разность a - b для беззнаковых; если b больше a, результат 0 (а не огромное число).
std::size_t distanceOrZero(std::size_t a, std::size_t b) {
    return a >= b ? a - b : 0;
}

// ----- Дальше основная часть программы: её менять не нужно -----

int main() {
    std::string line;
    while (std::getline(std::cin, line)) {
        std::string command = line.substr(0, line.find(' '));
        std::string rest = line.size() > command.size() ? line.substr(command.size() + 1) : "";
        std::istringstream in(rest);
        if (command == "parse") {
            std::optional<int> parsed = parseStrict(rest);
            if (parsed) {
                std::cout << *parsed << "\n";
            } else {
                std::cout << "none\n";
            }
        } else if (command == "add") {
            int a = 0;
            int b = 0;
            in >> a >> b;
            std::optional<int> sum = checkedAdd(a, b);
            if (sum) {
                std::cout << *sum << "\n";
            } else {
                std::cout << "overflow\n";
            }
        } else if (command == "div") {
            int a = 0;
            int b = 0;
            in >> a >> b;
            try {
                std::cout << divide(a, b) << "\n";
            } catch (const std::invalid_argument&) {
                std::cout << "invalid\n";
            } catch (const std::overflow_error&) {
                std::cout << "overflow\n";
            }
        } else if (command == "at") {
            std::size_t index = 0;
            in >> index;
            std::vector<int> v;
            for (int x; in >> x;) {
                v.push_back(x);
            }
            try {
                std::cout << elementAt(v, index) << "\n";
            } catch (const std::out_of_range&) {
                std::cout << "out of range\n";
            }
        } else if (command == "dist") {
            std::size_t a = 0;
            std::size_t b = 0;
            in >> a >> b;
            std::cout << distanceOrZero(a, b) << "\n";
        }
    }
}
