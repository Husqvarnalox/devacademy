#include <algorithm>
#include <cctype>
#include <iostream>
#include <iterator>
#include <ranges>
#include <string>
#include <string_view>
#include <unordered_map>
#include <utility>
#include <vector>

using Entry = std::pair<std::string, int>;

// Разбивает текст на слова из латинских букв и приводит их к нижнему регистру.
// string_view указывает внутрь text: text должен пережить все views.
std::vector<std::string> splitWords(const std::string& text) {
    std::vector<std::string> words;
    std::string_view rest = text;
    while (!rest.empty()) {
        std::size_t start = 0;
        while (start < rest.size() && !std::isalpha(static_cast<unsigned char>(rest[start]))) ++start;
        std::size_t end = start;
        while (end < rest.size() && std::isalpha(static_cast<unsigned char>(rest[end]))) ++end;
        if (end > start) {
            std::string w(rest.substr(start, end - start));
            // TODO: приведите слово к нижнему регистру (The и the - одно слово)
            words.push_back(std::move(w));
        }
        rest.remove_prefix(end);
    }
    return words;
}

int main() {
    std::size_t k;
    std::cin >> k;
    std::string text((std::istreambuf_iterator<char>(std::cin)), std::istreambuf_iterator<char>());

    std::unordered_map<std::string, int> freq;
    int total = 0;
    for (const std::string& w : splitWords(text)) {
        ++freq[w];
        ++total;
    }

    std::vector<Entry> items(freq.begin(), freq.end());
    // Порядок в unordered_map не определён, поэтому сортируем полностью: по убыванию частоты, затем по алфавиту.
    // TODO: при равной частоте слова должны идти по алфавиту
    std::ranges::sort(items, [](const Entry& a, const Entry& b) { return a.second > b.second; });

    for (const Entry& e : items | std::views::take(k)) std::cout << e.first << " " << e.second << "\n";
    std::cout << "distinct=" << items.size() << " total=" << total << "\n";
}
