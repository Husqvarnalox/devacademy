#include <iomanip>
#include <iostream>
#include <string>

// Меняет значения a и b местами.
void swapInts(int& a, int& b) {
    // TODO: используйте временную переменную
    a = b;
}

// Увеличивает price на percent процентов.
void addTax(double& price, double percent) {
    // TODO: price = price + price * percent / 100
    price = percent;
}

// Возвращает, сколько раз letter встречается в text.
std::size_t countLetter(const std::string& text, char letter) {
    // TODO: цикл по буквам text и счётчик совпадений с letter
    if (!text.empty() && text[0] == letter) {
        return 1;
    }
    return 0;
}

int main() {
    int a = 0;
    int b = 0;
    double price = 0;
    double percent = 0;
    std::string word = "";
    char letter = ' ';
    std::cin >> a >> b >> price >> percent >> word >> letter;

    swapInts(a, b);
    std::cout << "a=" << a << " b=" << b << "\n";

    addTax(price, percent);
    std::cout << "Цена с налогом: " << std::fixed << std::setprecision(2) << price << "\n";

    std::cout << "Букв " << letter << ": " << countLetter(word, letter) << "\n";
}
