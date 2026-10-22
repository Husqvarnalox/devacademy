#include <iostream>
#include <map>
#include <string>
#include <vector>

// Разбирает строку вида "имя;число". Возвращает false, если нет символа ';', имя пустое или после ';' нет цифр.
bool splitPair(const std::string& line, std::string& name, int& value) {
    // TODO: find(';') возвращает std::string::npos, если символа нет; имя и число вырежьте через substr, число переведите через std::stoi
    name = line;
    value = 0;
    return !line.empty();
}

// Суммирует amounts по категориям: categories[i] соответствует amounts[i].
std::map<std::string, int> totalsByCategory(const std::vector<std::string>& categories, const std::vector<int>& amounts) {
    // TODO: totals[categories[i]] += amounts[i] создаёт запись при первом обращении
    std::map<std::string, int> totals;
    if (!categories.empty() && !amounts.empty()) {
        totals[categories[0]] = amounts[0];
    }
    return totals;
}

// Категория с наибольшей суммой; при равенстве - первая по алфавиту; для пустого словаря - "-".
std::string biggest(const std::map<std::string, int>& totals) {
    // TODO: обход словаря идёт по алфавиту; обновляйте ответ только при строго большей сумме
    if (totals.empty()) {
        return "-";
    }
    return totals.begin()->first;
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
