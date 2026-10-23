#include <iostream>

int main() {
    int total = 0;
    std::cin >> total;

    int percent = 0;
    if (total >= 5000) {
        percent = 10;
    } else if (total >= 1000) {
        percent = 5;
    }

    int payable = total - total * percent / 100;
    std::cout << "Скидка: " << percent << "%\n";
    std::cout << "К оплате: " << payable << "\n";
}
