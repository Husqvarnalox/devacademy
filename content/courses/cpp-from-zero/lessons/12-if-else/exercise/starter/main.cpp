#include <iostream>

int main() {
    int total = 0;
    std::cin >> total;

    int percent = 0;
    // TODO: выберите процент скидки: 10 для суммы от 5000, 5 для суммы от 1000, иначе 0

    int payable = total - total * percent / 100;
    std::cout << "Скидка: " << percent << "%\n";
    std::cout << "К оплате: " << payable << "\n";
}
