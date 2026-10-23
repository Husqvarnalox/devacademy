#include <iostream>

int main() {
    // TODO: объявите переменные:
    //   temperature - дробное число 21.5 (double)
    //   rain        - логическое значение «идёт дождь»: true (bool)
    //   grade       - символ B (char)
    //   DAYS        - неизменяемое целое 7 (const int)

    std::cout << "Температура: " << temperature << "\n";
    // TODO: чтобы bool печатался словом true/false, добавьте std::boolalpha в цепочку перед rain
    std::cout << "Дождь: " << rain << "\n";
    std::cout << "Оценка: " << grade << "\n";
    std::cout << "Дней в неделе: " << DAYS << "\n";
}
