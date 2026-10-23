#include <iostream>
#include <map>
#include <string>
#include <vector>

// Индекс последнего вхождения target в values или -1, если числа нет.
int lastIndexOf(const std::vector<int>& values, int target) {
    for (std::size_t i = values.size(); i > 0; i--) {
        if (values[i - 1] == target) {
            return static_cast<int>(i - 1);
        }
    }
    return -1;
}

// Оставляет в values только неотрицательные числа в прежнем порядке.
void removeNegatives(std::vector<int>& values) {
    std::vector<int> kept;
    for (int value : values) {
        if (value >= 0) {
            kept.push_back(value);
        }
    }
    values = kept;
}

// Сколько раз встречается каждое слово.
std::map<std::string, int> countWords(const std::vector<std::string>& words) {
    std::map<std::string, int> counts;
    for (const std::string& word : words) {
        counts[word]++;
    }
    return counts;
}

// Средняя буква (нечётная длина) или две средние (чётная); для пустой строки - пустая строка.
std::string middle(const std::string& text) {
    if (text.empty()) {
        return "";
    }
    std::size_t half = text.size() / 2;
    if (text.size() % 2 == 1) {
        return text.substr(half, 1);
    }
    return text.substr(half - 1, 2);
}

int main() {
    std::string command;
    while (std::cin >> command) {
        if (command == "last") {
            int target = 0;
            std::size_t n = 0;
            std::cin >> target >> n;
            std::vector<int> values(n);
            for (std::size_t i = 0; i < n; i++) {
                std::cin >> values[i];
            }
            std::cout << lastIndexOf(values, target) << "\n";
        } else if (command == "clean") {
            std::size_t n = 0;
            std::cin >> n;
            std::vector<int> values(n);
            for (std::size_t i = 0; i < n; i++) {
                std::cin >> values[i];
            }
            removeNegatives(values);
            if (values.empty()) {
                std::cout << "empty\n";
            } else {
                for (std::size_t i = 0; i < values.size(); i++) {
                    std::cout << (i == 0 ? "" : " ") << values[i];
                }
                std::cout << "\n";
            }
        } else if (command == "words") {
            std::size_t n = 0;
            std::cin >> n;
            std::vector<std::string> words(n);
            for (std::size_t i = 0; i < n; i++) {
                std::cin >> words[i];
            }
            for (const auto& [word, count] : countWords(words)) {
                std::cout << word << "=" << count << "\n";
            }
        } else if (command == "mid") {
            std::string word;
            std::cin >> word;
            if (word == "-") {
                word = "";
            }
            std::cout << "[" << middle(word) << "]\n";
        }
    }
}
