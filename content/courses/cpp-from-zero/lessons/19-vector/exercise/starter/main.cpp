#include <iomanip>
#include <iostream>
#include <vector>

int main() {
    std::vector<int> numbers;
    // TODO: читайте числа до конца ввода и добавляйте их в numbers через push_back

    if (numbers.empty()) {
        std::cout << "Пусто\n";
        return 0;
    }

    int best = numbers[0];
    long long sum = 0;
    // TODO: обойдите numbers: найдите наибольшее и сумму

    double average = static_cast<double>(sum) / numbers.size();

    std::cout << "Количество: " << numbers.size() << "\n";
    std::cout << "Максимум: " << best << "\n";
    std::cout << "Среднее: " << std::fixed << std::setprecision(2) << average << "\n";

    std::cout << "Больше среднего:";
    // TODO: напечатайте числа строго больше average через пробел (после двоеточия),
    // а если таких нет - слово "нет"
    std::cout << "\n";
}
