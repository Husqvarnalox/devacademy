#include <algorithm>
#include <iostream>
#include <map>
#include <numeric>
#include <sstream>
#include <string>
#include <vector>

// Удаляет из вектора все отрицательные числа, сохраняя порядок остальных.
void removeNegatives(std::vector<int>& v) {
    // TODO: удалите отрицательные элементы (идиома erase-remove или std::erase_if)
    (void)v;
}

// Сортирует слова по длине, а слова одной длины - по алфавиту.
void sortWords(std::vector<std::string>& words) {
    // TODO: std::sort с лямбдой: сначала по длине, потом по алфавиту
    std::sort(words.begin(), words.end());
}

// Считает, сколько раз встретилось каждое слово.
std::map<std::string, int> countWords(const std::vector<std::string>& words) {
    // TODO: заполните std::map<std::string, int> числом вхождений каждого слова
    (void)words;
    return {};
}

// Сумма всех элементов. Результат может не помещаться в int.
long long total(const std::vector<int>& v) {
    // TODO: std::accumulate; подумайте, какого типа должно быть начальное значение
    (void)v;
    return 0;
}

// Сколько элементов строго больше limit.
int countAbove(const std::vector<int>& v, int limit) {
    // TODO: std::count_if с лямбдой, захватывающей limit
    (void)v;
    (void)limit;
    return 0;
}

// ----- Дальше основная часть программы: её менять не нужно -----

void printInts(const std::vector<int>& v) {
    std::cout << v.size() << ":";
    for (int x : v) {
        std::cout << " " << x;
    }
    std::cout << "\n";
}

int main() {
    std::string line;
    while (std::getline(std::cin, line)) {
        std::istringstream in(line);
        std::string command;
        in >> command;
        if (command == "clean") {
            std::vector<int> v;
            for (int x; in >> x;) {
                v.push_back(x);
            }
            removeNegatives(v);
            printInts(v);
        } else if (command == "sort") {
            std::vector<std::string> words;
            for (std::string w; in >> w;) {
                words.push_back(w);
            }
            sortWords(words);
            std::cout << words.size() << ":";
            for (const std::string& w : words) {
                std::cout << " " << w;
            }
            std::cout << "\n";
        } else if (command == "count") {
            std::vector<std::string> words;
            for (std::string w; in >> w;) {
                words.push_back(w);
            }
            std::map<std::string, int> counts = countWords(words);
            bool first = true;
            for (const auto& [word, n] : counts) {
                std::cout << (first ? "" : " ") << word << "=" << n;
                first = false;
            }
            std::cout << "\n";
        } else if (command == "stats") {
            int limit = 0;
            in >> limit;
            std::vector<int> v;
            for (int x; in >> x;) {
                v.push_back(x);
            }
            std::cout << "total=" << total(v) << " above=" << countAbove(v, limit) << "\n";
        }
    }
}
