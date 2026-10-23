#include <iostream>
#include <string>

int main() {
    int count = 0;
    int bestScore = 0;
    std::string bestName = "";

    // TODO: читайте строки до конца ввода, пропускайте пустые,
    // делите каждую по ';', печатайте "имя - балл" и обновляйте лучшего

    if (count == 0) {
        std::cout << "Данных нет\n";
        return 0;
    }
    std::cout << "Лучший: " << bestName << " (" << bestScore << ")\n";
}
