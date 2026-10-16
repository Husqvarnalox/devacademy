#include <iostream>
#include <string>

// TODO: заменить stoi на строгий разбор (std::from_chars + std::optional),
// проверять деление на ноль и переполнение, ловить исключения в main.
int apply(int a, char op, int b) {
    switch (op) {
        case '+': return a + b;
        case '-': return a - b;
        case '*': return a * b;
        case '/': return a / b;
        case '%': return a % b;
        default: return 0;
    }
}

int main() {
    int n = 0;
    std::cin >> n;
    for (int i = 0; i < n; ++i) {
        std::string x, op, y;
        std::cin >> x >> op >> y;
        std::cout << apply(std::stoi(x), op[0], std::stoi(y)) << "\n";
    }
}
