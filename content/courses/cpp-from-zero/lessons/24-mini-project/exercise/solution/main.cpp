#include <iomanip>
#include <iostream>
#include <map>
#include <stdexcept>
#include <string>

int parseScore(const std::string& text) {
    int value = std::stoi(text);
    if (std::to_string(value) != text) {
        throw std::invalid_argument("лишние символы в числе");
    }
    if (value < 0 || value > 100) {
        throw std::invalid_argument("балл вне диапазона 0-100");
    }
    return value;
}

int main() {
    std::map<std::string, int> scores;
    int skipped = 0;

    std::string line = "";
    while (std::getline(std::cin, line)) {
        if (line.empty()) {
            continue;
        }
        std::size_t pos = line.find(';');
        if (pos == std::string::npos || pos == 0) {
            skipped++;
            continue;
        }
        std::string name = line.substr(0, pos);
        if (scores.count(name) == 1) {
            skipped++;
            continue;
        }
        try {
            scores[name] = parseScore(line.substr(pos + 1));
        } catch (const std::exception&) {
            skipped++;
        }
    }

    std::cout << "Ведомость\n";
    int sum = 0;
    int passed = 0;
    std::string bestName = "";
    int bestScore = -1;
    for (const auto& [name, score] : scores) {
        std::cout << name << " - " << score << "\n";
        sum += score;
        if (score >= 60) {
            passed++;
        }
        if (score > bestScore) {
            bestScore = score;
            bestName = name;
        }
    }

    std::cout << "Принято: " << scores.size() << "\n";
    std::cout << "Пропущено: " << skipped << "\n";
    if (!scores.empty()) {
        std::cout << "Лучший: " << bestName << " (" << bestScore << ")\n";
        std::cout << "Среднее: " << std::fixed << std::setprecision(2)
                  << static_cast<double>(sum) / static_cast<double>(scores.size()) << "\n";
        std::cout << "Сдали: " << passed << " из " << scores.size() << "\n";
    }
}
