#include <iostream>
#include <string>

int main() {
    std::string word = "orange";

    std::cout << "Слово: " << word << "\n";
    std::cout << "Длина: " << word.size() << "\n";
    std::cout << "Первая буква: " << word[0] << "\n";
    std::cout << "Последняя буква: " << word[word.size() - 1] << "\n";
    std::cout << "С восклицанием: " << word + "!" << "\n";
}
