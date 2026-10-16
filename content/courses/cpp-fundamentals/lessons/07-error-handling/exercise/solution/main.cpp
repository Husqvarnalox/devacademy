#include <charconv>
#include <iostream>
#include <limits>
#include <optional>
#include <stdexcept>
#include <string>
#include <string_view>

// Строгий разбор: вся строка должна быть числом типа int.
std::optional<int> parseInt(std::string_view s) {
    if (s.empty()) return std::nullopt;
    int value = 0;
    const char* end = s.data() + s.size();
    auto [ptr, ec] = std::from_chars(s.data(), end, value);
    if (ec != std::errc{} || ptr != end) return std::nullopt;
    return value;
}

bool fitsInt(long long v) noexcept {
    return v >= std::numeric_limits<int>::min() && v <= std::numeric_limits<int>::max();
}

int apply(int a, char op, int b) {
    long long r = 0;
    switch (op) {
        case '+': r = static_cast<long long>(a) + b; break;
        case '-': r = static_cast<long long>(a) - b; break;
        case '*': r = static_cast<long long>(a) * b; break;
        case '/':
        case '%':
            if (b == 0) throw std::domain_error("division by zero");
            r = op == '/' ? static_cast<long long>(a) / b : static_cast<long long>(a) % b;
            break;
        default: throw std::invalid_argument("unknown operator");
    }
    if (!fitsInt(r)) throw std::overflow_error("overflow");
    return static_cast<int>(r);
}

int main() {
    int n = 0;
    std::cin >> n;
    for (int i = 0; i < n; ++i) {
        std::string x, op, y;
        std::cin >> x >> op >> y;
        try {
            auto a = parseInt(x);
            auto b = parseInt(y);
            if (!a || !b) throw std::invalid_argument("bad number");
            if (op.size() != 1) throw std::invalid_argument("unknown operator");
            std::cout << apply(*a, op[0], *b) << "\n";
        } catch (const std::exception& e) {
            std::cout << "error: " << e.what() << "\n";
        }
    }
}
