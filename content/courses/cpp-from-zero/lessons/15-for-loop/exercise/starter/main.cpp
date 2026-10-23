#include <iostream>
#include <string>

int main() {
    std::string word = "";
    std::cin >> word;

    int vowels = 0;
    // TODO: for по индексам слова; считайте буквы a, e, i, o, u

    std::string reversed = "";
    // TODO: for от конца слова к началу; дописывайте буквы в reversed

    std::cout << "Гласных: " << vowels << "\n";
    std::cout << "Наоборот: " << reversed << "\n";
}
