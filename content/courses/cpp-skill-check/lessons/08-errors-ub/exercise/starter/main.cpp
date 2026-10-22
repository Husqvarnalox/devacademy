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
    // TODO: std::from_chars; успех - это ec == std::errc() и ptr == конец строки
    (void)text;
    return std::nullopt;
}

// Сумма двух int; std::nullopt, если результат не помещается в int. Без неопределённого поведения.
std::optional<int> checkedAdd(int a, int b) {
    // TODO: проверьте переполнение ДО сложения: a + b при переполнении - неопределённое поведение
    (void)a;
    (void)b;
    return 0;
}

// Деление нацело. Бросает std::invalid_argument при делении на ноль
// и std::overflow_error, если результат не помещается в int (INT_MIN / -1).
int divide(int a, int b) {
    // TODO: бросайте std::invalid_argument при b == 0 и std::overflow_error для INT_MIN / -1
    (void)a;
    (void)b;
    return 0;
}

// Элемент по индексу с проверкой границ: при выходе за границы бросает std::out_of_range.
int elementAt(const std::vector<int>& v, std::size_t index) {
    // TODO: обратитесь к элементу так, чтобы выход за границы бросал std::out_of_range
    (void)v;
    (void)index;
    return 0;
}

// Разность a - b для беззнаковых; если b больше a, результат 0 (а не огромное число).
std::size_t distanceOrZero(std::size_t a, std::size_t b) {
    // TODO: если b больше a, верните 0
    return a - b;
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
