#include <iostream>
#include <map>
#include <string>
#include <vector>

// Индекс последнего вхождения target в values или -1, если числа нет.
int lastIndexOf(const std::vector<int>& values, int target) {
    // TODO: обход с конца; счётчик std::size_t не бывает меньше нуля - начните с values.size() и обращайтесь к values[i - 1]
    if (!values.empty() && values[0] == target) {
        return 0;
    }
    return -1;
}

// Оставляет в values только неотрицательные числа в прежнем порядке.
void removeNegatives(std::vector<int>& values) {
    // TODO: соберите подходящие числа в новый вектор и присвойте его обратно: values = kept;
    if (values.empty()) {
        return;
    }
}

// Сколько раз встречается каждое слово.
std::map<std::string, int> countWords(const std::vector<std::string>& words) {
    // TODO: counts[word]++ создаёт запись со значением 0 и сразу увеличивает её
    std::map<std::string, int> counts;
    if (words.empty()) {
        return counts;
    }
    return counts;
}

// Средняя буква (нечётная длина) или две средние (чётная); для пустой строки - пустая строка.
std::string middle(const std::string& text) {
    // TODO: длина пустой строки равна 0 - не вычисляйте text.size() - 1 без проверки
    return text;
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
