#include <iostream>
#include <map>
#include <string>

int main() {
    std::map<std::string, int> counts;
    int total = 0;

    std::string word = "";
    while (std::cin >> word) {
        counts[word]++;
        total++;
    }

    if (total == 0) {
        std::cout << "Слов нет\n";
        return 0;
    }

    for (const auto& [text, n] : counts) {
        std::cout << text << ": " << n << "\n";
    }

    std::cout << "Всего слов: " << total << "\n";
    std::cout << "Разных слов: " << counts.size() << "\n";
}
