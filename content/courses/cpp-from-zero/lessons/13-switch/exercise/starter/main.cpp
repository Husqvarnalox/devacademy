#include <iostream>
#include <string>

int main() {
    int day = 0;
    char answer = ' ';
    std::cin >> day >> answer;

    std::string dayText = "";
    // TODO: switch (day): 1-5 -> "Рабочий день", 6 и 7 -> "Выходной", иначе "Нет такого дня"

    std::string answerText = "";
    // TODO: switch (answer): 'y' и 'Y' -> "Да", 'n' и 'N' -> "Нет", иначе "Не понял"

    std::cout << dayText << "\n";
    std::cout << answerText << "\n";
}
