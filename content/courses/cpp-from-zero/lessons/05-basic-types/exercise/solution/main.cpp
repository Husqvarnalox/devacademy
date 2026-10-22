#include <iostream>

int main() {
    double temperature = 21.5;
    bool rain = true;
    char grade = 'B';
    const int DAYS = 7;

    std::cout << "Температура: " << temperature << "\n";
    std::cout << "Дождь: " << std::boolalpha << rain << "\n";
    std::cout << "Оценка: " << grade << "\n";
    std::cout << "Дней в неделе: " << DAYS << "\n";
}
