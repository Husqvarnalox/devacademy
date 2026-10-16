#include <cstdint>
#include <iostream>
#include <string>
#include <vector>

// Заготовка считает «как получится»: знаковые операции делаются через unsigned (определённое оборачивание),
// но переполнение, нулевой делитель, сдвиг и индекс не проверяются.
// TODO: возвращать "overflow", "div0", "bad shift" и "out of range" там, где результат не определён.
int wrap(std::uint32_t v) { return static_cast<int>(v); }

int main() {
    const std::vector<int> data{10, 20, 30, 40, 50};
    int n = 0;
    std::cin >> n;
    for (int i = 0; i < n; ++i) {
        std::string cmd;
        std::cin >> cmd;
        if (cmd == "add" || cmd == "sub" || cmd == "mul" || cmd == "div") {
            long long a, b;
            std::cin >> a >> b;
            auto ua = static_cast<std::uint32_t>(a);
            auto ub = static_cast<std::uint32_t>(b);
            if (cmd == "add") std::cout << wrap(ua + ub) << "\n";
            else if (cmd == "sub") std::cout << wrap(ua - ub) << "\n";
            else if (cmd == "mul") std::cout << wrap(ua * ub) << "\n";
            else std::cout << (b == 0 ? 0 : a / b) << "\n";
        } else if (cmd == "neg" || cmd == "abs") {
            long long a;
            std::cin >> a;
            auto ua = static_cast<std::uint32_t>(a);
            std::cout << (cmd == "neg" ? wrap(0u - ua) : (a < 0 ? wrap(0u - ua) : static_cast<int>(a))) << "\n";
        } else if (cmd == "shl") {
            long long a, k;
            std::cin >> a >> k;
            std::cout << wrap(static_cast<std::uint32_t>(a) << (k & 31)) << "\n";
        } else if (cmd == "at") {
            long long idx;
            std::cin >> idx;
            std::cout << data[static_cast<std::size_t>(idx) % data.size()] << "\n";
        }
    }
}
