#include <iomanip>
#include <iostream>
#include <string>

// Меняет значения a и b местами.
void swapInts(int& a, int& b) {
    int temp = a;
    a = b;
    b = temp;
}

// Увеличивает price на percent процентов.
void addTax(double& price, double percent) {
    price = price + price * percent / 100;
}

// Возвращает, сколько раз letter встречается в text.
std::size_t countLetter(const std::string& text, char letter) {
    std::size_t count = 0;
    for (std::size_t i = 0; i < text.size(); i++) {
        if (text[i] == letter) {
            count++;
        }
    }
    return count;
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
