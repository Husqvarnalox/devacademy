#include <iostream>
#include <map>
#include <string>
#include <vector>

// Разбирает строку вида "имя;число". Возвращает false, если нет символа ';', имя пустое или после ';' нет цифр.
bool splitPair(const std::string& line, std::string& name, int& value) {
    std::size_t semicolon = line.find(';');
    if (semicolon == std::string::npos || semicolon == 0 || semicolon + 1 >= line.size()) {
        return false;
    }
    name = line.substr(0, semicolon);
    value = std::stoi(line.substr(semicolon + 1));
    return true;
}

// Суммирует amounts по категориям: categories[i] соответствует amounts[i].
std::map<std::string, int> totalsByCategory(const std::vector<std::string>& categories, const std::vector<int>& amounts) {
    std::map<std::string, int> totals;
    for (std::size_t i = 0; i < categories.size(); i++) {
        totals[categories[i]] += amounts[i];
    }
    return totals;
}

// Категория с наибольшей суммой; при равенстве - первая по алфавиту; для пустого словаря - "-".
std::string biggest(const std::map<std::string, int>& totals) {
    std::string best = "-";
    int bestValue = 0;
    for (const auto& [name, total] : totals) {
        if (best == "-" || total > bestValue) {
            best = name;
            bestValue = total;
        }
    }
    return best;
}

int main() {
    std::vector<std::string> categories;
    std::vector<int> amounts;
    int skipped = 0;
    std::string line;
    while (std::getline(std::cin, line)) {
        std::string name;
        int value = 0;
        if (!splitPair(line, name, value)) {
            skipped++;
            continue;
        }
        categories.push_back(name);
        amounts.push_back(value);
    }
    std::map<std::string, int> totals = totalsByCategory(categories, amounts);
    for (const auto& [name, total] : totals) {
        std::cout << name << ": " << total << "\n";
    }
    std::cout << "top: " << biggest(totals) << "\n";
    std::cout << "skipped: " << skipped << "\n";
}
