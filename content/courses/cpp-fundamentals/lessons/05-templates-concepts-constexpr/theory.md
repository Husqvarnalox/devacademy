<!-- section: simple -->
## Шаблоны: код, который пишется один раз для многих типов

В Java и C# обобщения проверяются один раз, а в рантайме часто работают с одним и тем же кодом. В C++ **шаблон** (template) - это рецепт, по которому компилятор **генерирует отдельную функцию или класс для каждого набора типов**, с которым шаблон использован. Это называется **инстанцированием**. Отсюда сильные и слабые стороны:

* нет накладных расходов во время выполнения (`std::vector<int>` работает как рукописный код для `int`);
* типы не обязаны иметь общий базовый класс или интерфейс: достаточно, чтобы операции в теле шаблона были применимы;
* ошибки обнаруживаются при инстанцировании, и в старых версиях языка сообщения о них были огромными.

**Концепты** (C++20) решают последнюю проблему: вы явно записываете требования к типу (`std::integral`, `std::totally_ordered`, свой `Numeric`), и компилятор проверяет их **до** разбора тела, выдавая понятное сообщение.

**`constexpr`** позволяет выполнять обычные функции во время компиляции, если их аргументы известны. Результат можно использовать там, где нужна константа: размер массива, аргумент шаблона, `static_assert`. Одна и та же функция работает и во время компиляции, и во время выполнения.

<!-- section: precise -->
## Точные правила

**Шаблоны функций и классов.** `template <typename T> T maxOf(T a, T b)` - шаблон функции; `template <typename T> class Stack` - шаблон класса. Параметры шаблона бывают типами, значениями (`template <std::size_t N>`) и другими шаблонами. Для функций тип обычно **выводится** из аргументов (`maxOf(1, 2)`), можно указать явно (`maxOf<double>(1, 2)`). Для классов с C++17 работает **вывод аргументов шаблона класса (CTAD)**: `std::pair p(1, 2.5);`, `std::vector v{1, 2, 3};` (в `-std=c++14` не компилируется).

**Определения в заголовках.** Шаблон должен быть виден компилятору в точке инстанцирования, поэтому его определение обычно лежит в заголовке. Для ODR (см. [первый урок](lesson:cpp.fundamentals.compilation-lifetime-storage)) это допустимо: шаблоны могут определяться в нескольких единицах трансляции. Каждая инстанциация - самостоятельный тип или функция: у `Counter<int>` и `Counter<double>` свои статические члены.

**Перегрузка и специализация.** Обычная (не шаблонная) функция предпочтительнее шаблона при прочих равных. Шаблон классов можно специализировать полностью или частично; шаблоны функций лучше не специализировать, а перегружать.

**`if constexpr` (C++17).** Условие вычисляется при компиляции, ветка, не прошедшая проверку, в шаблоне **не инстанцируется**: в ней можно использовать операции, недоступные другим типам.

```cpp
template <typename T>
auto describe(const T& v) {
    if constexpr (std::is_integral_v<T>) return "int";  // для T = std::string эта ветка не разбирается
    else return "other";
}
```

**Вариативные шаблоны и свёртки (C++17).** `template <typename... Args> auto sum(Args... args) { return (args + ...); }` - свёртка раскрывает пакет параметров с оператором.

**Концепты (C++20).** Концепт - именованный предикат над типами: `template <typename T> concept Numeric = std::integral<T> || std::floating_point<T>;`. Использовать его можно тремя способами: вместо `typename` (`template <Numeric T>`), в `requires`-клаузе (`requires std::convertible_to<T, std::string_view>`) или как сокращённую запись (`void f(Numeric auto x)`). Функции с разными ограничениями перегружаются: выбирается удовлетворяющая и наиболее ограниченная (при условии, что остальные части сигнатуры совпадают; иначе вызов окажется неоднозначным). Стандартные концепты лежат в `<concepts>` (`integral`, `floating_point`, `convertible_to`, `same_as`, `totally_ordered`, `copyable`). Внутри концепта выражение `requires (T a, T b) { a + b; }` проверяет корректность выражений. В режиме `-std=c++17` ключевое слово `concept` не компилируется.

**`constexpr` и `consteval`.**

* `constexpr`-функция (C++11, с циклами и локальными переменными с C++14) может вызываться и во время компиляции, и во время выполнения. Если контекст требует константу (`static_assert`, размер массива, аргумент шаблона), вычисление должно уложиться в компиляцию: нельзя ввод-вывод, `new` без освобождения в той же константе, неопределённое поведение. Компилятор **диагностирует** неопределённое поведение в константном выражении как ошибку - это удобный способ его ловить.
* `consteval` (C++20) - «непосредственная функция»: каждый вызов обязан быть константным выражением.
* `constexpr`-переменная - константа, вычисленная при компиляции; `constinit` (C++20) гарантирует статическую инициализацию без требования константности.
* `std::is_constant_evaluated()` (C++20) позволяет различить режимы внутри функции.
* `std::array` и многие функции стандартной библиотеки (`std::sort` с C++20, `std::accumulate` с C++20) тоже `constexpr`.

**Версии (проверено компиляцией).** CTAD, `if constexpr` и свёртки - C++17; концепты, `consteval` и `<=>` - C++20; `constexpr` с циклами - C++14 (в `-std=c++11` не компилируется).

**Цена шаблонов.** Каждая инстанциация - дополнительный код (раздувание двоичного файла), а время компиляции растёт. Для уменьшения дублирования выносите нешаблонную часть в обычные функции.

<!-- section: examples -->
## Примеры

**Пример 1. Шаблон функции, вывод типа и CTAD.**

```cpp run
#include <iostream>
#include <string>
#include <utility>
#include <vector>

template <typename T>
T maxOf(T a, T b) {
    return a < b ? b : a;
}

const char* maxOf(const char* a, const char* b) {  // обычная перегрузка: сравниваем строки, а не адреса
    return std::string(a) < std::string(b) ? b : a;
}

int main() {
    std::cout << maxOf(3, 7) << " " << maxOf(2.5, 1.5) << " " << maxOf<double>(1, 2.5) << "\n";
    std::cout << maxOf(std::string("pear"), std::string("apple")) << " " << maxOf("kiwi", "plum") << "\n";
    std::pair p(1, 2.5);
    std::vector v{4, 5, 6};
    std::cout << p.first << " " << p.second << " " << v.size() << "\n";
}
```
```output
7 2.5 2.5
pear plum
1 2.5 3
```

`maxOf(3, 7)` и `maxOf(2.5, 1.5)` - две разные сгенерированные функции. Вызов с `const char*` выбрал обычную перегрузку.

**Пример 2. Шаблон класса и отдельная статика у каждой инстанциации.**

```cpp run
#include <iostream>
#include <string>
#include <vector>

template <typename T>
class Stack {
public:
    void push(T v) { items_.push_back(std::move(v)); }
    T pop() {
        T v = std::move(items_.back());
        items_.pop_back();
        return v;
    }
    bool empty() const { return items_.empty(); }
    static inline int instances = 0;
    Stack() { ++instances; }

private:
    std::vector<T> items_;
};

int main() {
    Stack<int> a, b;
    Stack<std::string> s;
    a.push(1);
    a.push(2);
    s.push("x");
    std::cout << a.pop() << " " << s.pop() << " " << a.empty() << "\n";
    std::cout << Stack<int>::instances << " " << Stack<std::string>::instances << "\n";
    (void)b;
}
```
```output
2 x 0
2 1
```

`Stack<int>` и `Stack<std::string>` - два несвязанных класса, у каждого своя статическая переменная.

**Пример 3. `if constexpr` и свёртка.**

```cpp run
#include <iostream>
#include <string>
#include <type_traits>

template <typename T>
std::string describe(const T& v) {
    if constexpr (std::is_integral_v<T>) {
        return "integer " + std::to_string(v);
    } else if constexpr (std::is_floating_point_v<T>) {
        return "float " + std::to_string(static_cast<int>(v * 10));
    } else {
        return "text " + std::string(v);  // для int эта ветка даже не разбирается
    }
}

template <typename... Args>
auto sum(Args... args) {
    return (args + ...);
}

int main() {
    std::cout << describe(42) << ", " << describe(2.5) << ", " << describe("hi") << "\n";
    std::cout << sum(1, 2, 3, 4) << " " << sum(1.5, 2.5) << "\n";
}
```
```output
integer 42, float 25, text hi
10 4
```

**Пример 4. Концепты и перегрузка по ограничениям.**

```cpp run
#include <concepts>
#include <iostream>
#include <string>
#include <string_view>
#include <vector>

template <typename T>
concept Numeric = std::integral<T> || std::floating_point<T>;

template <typename T>
concept Printable = requires(std::ostream& out, const T& v) { out << v; };

template <Numeric T>
std::string kind(const T&) { return "number"; }

template <typename T>
    requires std::convertible_to<T, std::string_view>
std::string kind(const T&) { return "text"; }

template <typename T>
std::string kind(const T&) { return "something else"; }  // самый общий вариант, без ограничений

template <Printable T>
void show(const T& v) { std::cout << "[" << v << "]\n"; }

int main() {
    std::cout << kind(1) << ", " << kind(2.5) << ", " << kind(std::string("a")) << ", " << kind("lit") << ", "
              << kind(std::vector<int>{}) << "\n";
    show(42);
    show(std::string("ok"));
    // show(std::vector<int>{});  // ошибка: ограничение Printable не удовлетворено
}
```
```output
number, number, text, text, something else
[42]
[ok]
```

Закомментированная строка не компилируется с понятным сообщением вроде `constraints not satisfied`, в старом стиле (без концептов) вы получили бы страницу ошибок из недр `operator<<`.

**Пример 5. `constexpr`: одна функция для компиляции и выполнения.**

```cpp run
#include <array>
#include <iostream>
#include <type_traits>

constexpr long long factorial(int n) {
    long long r = 1;
    for (int i = 2; i <= n; ++i) r *= i;
    return r;
}

constexpr auto makeSquares() {
    std::array<int, 6> a{};
    for (int i = 0; i < 6; ++i) a[i] = i * i;
    return a;
}

constexpr std::array<int, 6> kSquares = makeSquares();  // вычислено компилятором
static_assert(factorial(10) == 3628800);
static_assert(kSquares[5] == 25);

constexpr const char* where() { return std::is_constant_evaluated() ? "compile time" : "run time"; }

consteval int cube(int x) { return x * x * x; }

int main() {
    int runtimeValue = 5;
    std::cout << factorial(5) << " " << factorial(runtimeValue) << "\n";
    std::cout << kSquares[3] << " " << cube(3) << "\n";
    constexpr const char* a = where();
    const char* b = where();
    std::cout << a << ", " << b << "\n";
}
```
```output
120 120
9 27
compile time, run time
```

`factorial(runtimeValue)` исполняется как обычная функция. `where()` при вычислении в `constexpr`-переменной сообщает о времени компиляции, а в обычной - о времени выполнения. Ошибка в `static_assert` - ошибка сборки, а не запуска.

**Пример 6. Неопределённое поведение в `constexpr` - ошибка компиляции (не компилируется).**

```cpp
constexpr int divide(int a, int b) { return a / b; }
constexpr int bad = divide(1, 0);      // ошибка: деление на ноль не является константным выражением

constexpr int overflow = 2147483647 + 1;   // ошибка: переполнение знакового int
```

Это удобный способ обнаруживать неопределённое поведение: в константном контексте компилятор обязан его диагностировать.

<!-- section: mistakes -->
## Типичные ошибки

* **Ожидать, что тело шаблона проверяется один раз.** Ошибка в коде для конкретного типа проявится только при инстанцировании; концепты проверяют требования раньше.
* **Путать тип начального значения `std::accumulate`.** `std::accumulate(v.begin(), v.end(), 0)` складывает в `int`, даже если `v` - вектор `double`: дробные части теряются. Передавайте `0.0` или `T{}`.
* **Писать определение шаблона в `.cpp` и использовать из другого файла.** Компоновщик не найдёт нужную инстанциацию; определения шаблонов размещают в заголовках.
* **Специализировать шаблоны функций.** Специализации не участвуют в разрешении перегрузок так, как ожидается; используйте перегрузки и `if constexpr`.
* **Забыть, что `constexpr` не значит «вычислено при компиляции».** Если вызов не в константном контексте, функция может выполниться при запуске. Для гарантии используйте `constexpr`-переменную, `consteval` или `static_assert`.
* **Бесконтрольно усложнять ограничения.** Слишком хитрые концепты читаются хуже, чем SFINAE; стремитесь к стандартным концептам и коротким собственным.
* **Игнорировать раздувание кода.** Много инстанциаций с разными типами - большие бинарные файлы и долгая сборка.
* **Использовать `auto` в параметрах без ограничения.** `void f(auto x)` принимает всё; добавляйте концепт (`Numeric auto x`).

<!-- section: summary -->
## Итоги

* Шаблон - рецепт, компилятор генерирует отдельный код для каждого набора типов; издержки во время выполнения нулевые, но растёт время компиляции и размер.
* Концепты (C++20) выражают требования к типам, дают понятные ошибки и позволяют перегружать по ограничениям; `if constexpr` и свёртки (C++17) убирают ветвления и рекурсию из шаблонов.
* `constexpr` выполняет обычные функции при компиляции, `consteval` требует этого, `static_assert` проверяет результат; неопределённое поведение в константном выражении - ошибка компиляции.
* Версии проверены ключами `-std=c++11`, `14`, `17`, `20`.

Дальше: [контейнеры и алгоритмы STL](lesson:cpp.fundamentals.stl-containers-algorithms).
Назад: [классы, наследование и виртуальные функции](lesson:cpp.fundamentals.classes-inheritance-virtual).
Сравните: [трейты и обобщения в Rust](lesson:rust.fundamentals.traits-and-generics), [обобщения и коллекции в C#](lesson:csharp.fundamentals.generics-and-collections), [обобщения и коллекции в Java](lesson:java.fundamentals.generics-collections).
