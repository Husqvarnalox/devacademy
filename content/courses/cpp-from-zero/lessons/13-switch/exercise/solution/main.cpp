#include <iostream>
#include <string>

int main() {
    int day = 0;
    char answer = ' ';
    std::cin >> day >> answer;

    std::string dayText = "";
    switch (day) {
        case 1:
        case 2:
        case 3:
        case 4:
        case 5:
            dayText = "Рабочий день";
            break;
        case 6:
        case 7:
            dayText = "Выходной";
            break;
        default:
            dayText = "Нет такого дня";
    }

    std::string answerText = "";
    switch (answer) {
        case 'y':
        case 'Y':
            answerText = "Да";
            break;
        case 'n':
        case 'N':
            answerText = "Нет";
            break;
        default:
            answerText = "Не понял";
    }

    std::cout << dayText << "\n";
    std::cout << answerText << "\n";
}
