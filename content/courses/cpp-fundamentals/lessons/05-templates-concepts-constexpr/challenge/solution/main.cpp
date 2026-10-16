#include <array>
#include <cstddef>
#include <iostream>
#include <string>

using u64 = unsigned long long;

// Квадратная матрица N x N с арифметикой по модулю. Всё вычисляется на этапе компиляции,
// если аргументы известны, и во время выполнения в остальных случаях.
template <std::size_t N>
struct Mat {
    std::array<std::array<u64, N>, N> a{};

    static constexpr Mat identity(u64 mod) {
        Mat r;
        for (std::size_t i = 0; i < N; ++i) r.a[i][i] = 1 % mod;
        return r;
    }

    constexpr Mat mul(const Mat& o, u64 mod) const {
        Mat r;
        for (std::size_t i = 0; i < N; ++i)
            for (std::size_t j = 0; j < N; ++j) {
                u64 s = 0;
                for (std::size_t k = 0; k < N; ++k) s = (s + a[i][k] % mod * (o.a[k][j] % mod)) % mod;
                r.a[i][j] = s;
            }
        return r;
    }

    constexpr Mat pow(u64 e, u64 mod) const {
        Mat result = identity(mod);
        Mat base = *this;
        while (e > 0) {
            if (e & 1) result = result.mul(base, mod);
            base = base.mul(base, mod);
            e >>= 1;
        }
        return result;
    }
};

// F(0) = 0, F(1) = 1, F(n) = F(n-1) + F(n-2)
constexpr u64 fib(u64 n, u64 mod) {
    Mat<2> m;
    m.a = {{{1, 1}, {1, 0}}};
    return m.pow(n, mod).a[0][1];
}

// T(0) = 0, T(1) = 0, T(2) = 1, T(n) = T(n-1) + T(n-2) + T(n-3)
constexpr u64 trib(u64 n, u64 mod) {
    Mat<3> m;
    m.a = {{{1, 1, 1}, {1, 0, 0}, {0, 1, 0}}};
    return m.pow(n, mod).a[2][0];
}

static_assert(fib(10, 1000) == 55);
static_assert(fib(0, 7) == 0);
static_assert(trib(10, 1000) == 81);
static_assert(trib(2, 1000) == 1);

int main() {
    int q;
    std::cin >> q;
    for (int i = 0; i < q; ++i) {
        std::string kind;
        u64 n, mod;
        std::cin >> kind >> n >> mod;
        if (kind == "fib") std::cout << fib(n, mod) << "\n";
        else if (kind == "trib") std::cout << trib(n, mod) << "\n";
        else std::cout << "error\n";
    }
}
