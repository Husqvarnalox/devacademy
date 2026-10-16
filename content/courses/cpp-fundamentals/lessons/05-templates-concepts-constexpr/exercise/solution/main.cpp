#include <algorithm>
#include <concepts>
#include <iomanip>
#include <iostream>
#include <numeric>
#include <sstream>
#include <string>
#include <string_view>
#include <type_traits>
#include <vector>

template <typename T>
concept Numeric = std::integral<T> || std::floating_point<T>;

// Тип накопителя: целые суммируем в long long, вещественные в double.
template <Numeric T>
using Accumulator = std::conditional_t<std::integral<T>, long long, double>;

template <std::totally_ordered T>
std::pair<T, T> minMax(const std::vector<T>& v) {
    auto [lo, hi] = std::minmax_element(v.begin(), v.end());
    return {*lo, *hi};
}

template <Numeric T>
void report(const std::vector<T>& v) {
    auto [lo, hi] = minMax(v);
    Accumulator<T> sum = std::accumulate(v.begin(), v.end(), Accumulator<T>{});
    double mean = static_cast<double>(sum) / static_cast<double>(v.size());
    std::ostringstream formattedMean;  // отдельный поток: форматирование не «залипает» в std::cout
    formattedMean << std::fixed << std::setprecision(2) << mean;
    std::cout << "count=" << v.size() << " min=" << lo << " max=" << hi << " sum=" << sum
              << " mean=" << formattedMean.str() << "\n";
}

template <typename T>
    requires std::convertible_to<T, std::string_view>
void report(const std::vector<T>& v) {
    auto [lo, hi] = minMax(v);
    std::size_t chars = 0;
    for (const T& s : v) chars += std::string_view(s).size();
    std::cout << "count=" << v.size() << " min=" << lo << " max=" << hi << " chars=" << chars << "\n";
}

template <typename T>
void process(std::istream& in) {
    std::size_t n;
    in >> n;
    std::vector<T> v(n);
    for (T& x : v) in >> x;
    if (v.empty()) {
        std::cout << "count=0 empty\n";
        return;
    }
    report(v);
}

int main() {
    int lines;
    std::cin >> lines;
    for (int i = 0; i < lines; ++i) {
        std::string type;
        std::cin >> type;
        if (type == "int") process<int>(std::cin);
        else if (type == "double") process<double>(std::cin);
        else if (type == "str") process<std::string>(std::cin);
        else std::cout << "error: unknown type " << type << "\n";
    }
}
