#include <iostream>
#include <string>

int main() {
    std::string word = "";
    std::cin >> word;

    int vowels = 0;
    for (std::size_t i = 0; i < word.size(); i++) {
        char c = word[i];
        if (c == 'a' || c == 'e' || c == 'i' || c == 'o' || c == 'u') {
            vowels++;
        }
    }

    std::string reversed = "";
    for (std::size_t i = word.size(); i > 0; i--) {
        reversed += word[i - 1];
    }

    std::cout << "Гласных: " << vowels << "\n";
    std::cout << "Наоборот: " << reversed << "\n";
}
