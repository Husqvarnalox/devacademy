#include <iomanip>
#include <iostream>
#include <vector>

int main() {
    std::vector<int> numbers;
    int x = 0;
    while (std::cin >> x) {
        numbers.push_back(x);
    }

    if (numbers.empty()) {
        std::cout << "Пусто\n";
        return 0;
    }

    int best = numbers[0];
    long long sum = 0;
    for (int value : numbers) {
        sum += value;
        if (value > best) {
            best = value;
        }
    }

    double average = static_cast<double>(sum) / numbers.size();

    std::cout << "Количество: " << numbers.size() << "\n";
    std::cout << "Максимум: " << best << "\n";
    std::cout << "Среднее: " << std::fixed << std::setprecision(2) << average << "\n";

    std::cout << "Больше среднего:";
    int found = 0;
    for (int value : numbers) {
        if (value > average) {
            std::cout << " " << value;
            found++;
        }
    }
    if (found == 0) {
        std::cout << " нет";
    }
    std::cout << "\n";
}
