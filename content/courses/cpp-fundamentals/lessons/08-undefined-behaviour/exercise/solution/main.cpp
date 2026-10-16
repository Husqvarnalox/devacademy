#include <climits>
#include <iostream>
#include <optional>
#include <string>
#include <vector>

// Все операции определены для любых int: вместо неопределённого поведения возвращаем причину отказа.
struct Result {
    std::optional<int> value;
    const char* error = nullptr;
};

Result ok(long long v) {
    if (v < INT_MIN || v > INT_MAX) return {std::nullopt, "overflow"};
    return {static_cast<int>(v), nullptr};
}

Result fail(const char* why) { return {std::nullopt, why}; }

int main() {
    const std::vector<int> data{10, 20, 30, 40, 50};
    int n = 0;
    std::cin >> n;
    for (int i = 0; i < n; ++i) {
        std::string cmd;
        std::cin >> cmd;
        Result r;
        if (cmd == "add" || cmd == "sub" || cmd == "mul" || cmd == "div") {
            long long a, b;
            std::cin >> a >> b;
            if (cmd == "add") r = ok(a + b);
            else if (cmd == "sub") r = ok(a - b);
            else if (cmd == "mul") r = ok(a * b);
            else r = b == 0 ? fail("div0") : ok(a / b);
        } else if (cmd == "neg" || cmd == "abs") {
            long long a;
            std::cin >> a;
            r = ok(cmd == "neg" ? -a : (a < 0 ? -a : a));
        } else if (cmd == "shl") {
            long long a, k;
            std::cin >> a >> k;
            r = (k < 0 || k >= 32) ? fail("bad shift") : ok(a * (1LL << k));
        } else if (cmd == "at") {
            long long idx;
            std::cin >> idx;
            r = (idx < 0 || idx >= static_cast<long long>(data.size())) ? fail("out of range") : ok(data[static_cast<std::size_t>(idx)]);
        } else {
            r = fail("unknown");
        }
        if (r.value) std::cout << *r.value << "\n";
        else std::cout << r.error << "\n";
    }
}
