#include <iostream>
#include <map>
#include <string>

int main() {
    std::map<std::string, int> counts;
    int total = 0;

    // TODO: читайте слова до конца ввода: увеличивайте counts[word] и total

    if (total == 0) {
        std::cout << "Слов нет\n";
        return 0;
    }

    // TODO: обойдите counts и напечатайте строки вида "слово: количество"

    std::cout << "Всего слов: " << total << "\n";
    std::cout << "Разных слов: " << counts.size() << "\n";
}
