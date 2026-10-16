<!-- section: simple -->
## Как сообщать об ошибках в C++

В C++ нет единственного «правильного» механизма ошибок. Есть несколько, и у каждого своя область применения:

* **Исключения** (`throw`/`try`/`catch`) - для ошибок, которые вызывающий код обычно не может обработать на месте: нет файла, нарушен инвариант, не хватило памяти. Исключение раскручивает стек и вызывает деструкторы локальных объектов (поэтому [RAII](lesson:cpp.fundamentals.raii-smart-pointers) и исключения работают вместе).
* **`std::optional<T>`** (C++17) - «значения может не быть», когда это нормальный исход: поиск, разбор строки.
* **`std::expected<T, E>`** (C++23) - «значение или причина ошибки», когда ошибка ожидаема и вызывающий должен её разобрать.
* **Коды возврата** (`std::error_code`, `std::errc`) - так устроены `std::from_chars` и функции файловой системы без исключений.
* **`noexcept`** - обещание, что функция не бросает исключений. От него зависит, будет ли `vector` перемещать элементы при росте или копировать.
* **`assert`/`terminate`** - для ошибок программиста, которые нельзя исправить: нарушены предусловия.

Правило выбора: ошибка, которую вызывающий обработает рядом, - `optional`/`expected`/код; исключительная ситуация, которую нужно «пронести» через много слоёв, - исключение; нарушение собственного инварианта - `assert`.

Для сравнения с другими языками: Rust выбирает `Result` и `Option`, Go - возврат `error`, Java и C# - исключения.

<!-- section: precise -->
## Точные правила

**Исключения.**

* `throw expr;` копирует (или перемещает) значение в специальную область памяти; тип может быть любым, но принято бросать классы, производные от `std::exception` (`std::runtime_error`, `std::invalid_argument`, `std::out_of_range`, `std::overflow_error`, `std::domain_error`).
* Ловите **по константной ссылке**: `catch (const std::exception& e)`. Ловля по значению приводит к срезке (slicing) производного типа; обработчики проверяются сверху вниз, так что более частный тип ставят раньше общего. `catch (...)` ловит всё.
* `throw;` внутри обработчика перебрасывает текущее исключение без копирования и срезки.
* При раскрутке стека вызываются деструкторы полностью построенных локальных объектов. Если конструктор бросил исключение, деструктор самого объекта не вызывается, а деструкторы его уже построенных подобъектов и полей - вызываются.
* Деструкторы по умолчанию `noexcept`. Исключение, вылетевшее из деструктора во время раскрутки, ведёт к `std::terminate`.
* Необработанное исключение вызывает `std::terminate`, программа завершается аварийно.
* Цена: на «обычном» пути почти бесплатно, а бросок и раскрутка заметно дороже обычного возврата. Поэтому исключения не используют для управления потоком в горячих циклах.

**`noexcept`.**

* `void f() noexcept;` - обещание, что исключение из `f` не выйдет. Если выйдет - вызывается `std::terminate` (раскрутка не обязана выполняться).
* `noexcept(expr)` - оператор: проверяет на этапе компиляции, объявлено ли выражение как не бросающее.
* Конструктор перемещения и `swap` должны быть `noexcept`. Иначе `std::vector` при росте использует `std::move_if_noexcept`, то есть **копирует** элементы, чтобы сохранить сильную гарантию при исключении (см. [урок про перемещение](lesson:cpp.fundamentals.references-pointers-move)).
* `noexcept` является частью типа функции с C++17.

**Гарантии безопасности исключений** (для операции над объектом): *базовая* - инварианты сохранены, утечек нет; *сильная* - при исключении состояние как до операции (commit-or-rollback); *отсутствие исключений* (`nothrow`) - для `swap`, перемещения, деструктора. Идиома copy-and-swap даёт сильную гарантию присваивания.

**`std::optional<T>`** (C++17) хранит значение или ничего. `*opt` и `opt->` без проверки - неопределённое поведение при пустом; `opt.value()` бросает `std::bad_optional_access`; `value_or(x)` возвращает запасное значение. Подходит как результат, когда причина отсутствия не нужна.

**`std::from_chars`** (C++17, `<charconv>`) разбирает число без исключений, без локали и без выделения памяти. Возвращает пару: указатель на первый неразобранный символ и код `std::errc`. Не пропускает пробелы и знак `+`. Чтобы принять всю строку целиком, проверяйте и код, и то, что указатель дошёл до конца. Для сравнения, `std::stoi` разбирает только префикс (`"12abc"` даёт 12), пропускает пробелы и бросает исключения `invalid_argument` и `out_of_range`.

**`std::expected<T, E>`** (C++23, `<expected>`) хранит значение `T` или ошибку `E`. Возвращает ошибку через `std::unexpected(e)`; проверяется как `if (r)`, значение - `*r`, ошибка - `r.error()`. Монадические операции `and_then`, `transform`, `or_else` позволяют строить цепочки без вложенных `if`. В режиме `-std=c++20` заголовок `<expected>` недоступен (`std::expected` не объявлен), поэтому в этом курсе такой код показан в блоках без запуска: среда выполнения собирает программы с `-std=c++20`. Пример проверен компиляцией с `-std=c++23`.

**Что выбрать.** Не бросайте исключения из деструкторов и `noexcept`-функций. Не используйте исключения для ожидаемых исходов (конец ввода, «не найдено»). Не глотайте ошибки молча (`catch (...) {}`).

<!-- section: examples -->
## Примеры

**Пример 1. Раскрутка стека и RAII.**

```cpp run
#include <iostream>
#include <stdexcept>
#include <string>

struct Guard {
    std::string name;
    explicit Guard(std::string n) : name(std::move(n)) { std::cout << "acquire " << name << "\n"; }
    ~Guard() { std::cout << "release " << name << "\n"; }
};

void inner(int depth) {
    Guard g("inner" + std::to_string(depth));
    if (depth == 2) throw std::runtime_error("failure at depth 2");
    inner(depth + 1);
    std::cout << "unreachable for depth " << depth << "\n";
}

int main() {
    try {
        Guard outer("outer");
        inner(1);
    } catch (const std::runtime_error& e) {
        std::cout << "caught: " << e.what() << "\n";
    }
    std::cout << "done\n";
}
```
```output
acquire outer
acquire inner1
acquire inner2
release inner2
release inner1
release outer
caught: failure at depth 2
done
```

Деструкторы вызваны в обратном порядке создания, до входа в обработчик. Строки `unreachable ...` не напечатаны.

**Пример 2. Свои исключения, порядок обработчиков и `throw;`.**

```cpp run
#include <iostream>
#include <stdexcept>
#include <string>

struct ConfigError : std::runtime_error {
    int line;
    ConfigError(const std::string& msg, int l) : std::runtime_error(msg), line(l) {}
};

void load(int what) {
    if (what == 1) throw ConfigError("missing key", 7);
    if (what == 2) throw std::out_of_range("index");
    if (what == 3) throw 42;
}

void attempt(int what) {
    try {
        try {
            load(what);
            std::cout << "ok\n";
        } catch (const ConfigError& e) {      // частный тип раньше общего
            std::cout << "config error at line " << e.line << ": " << e.what() << "\n";
        } catch (const std::exception& e) {   // всё прочее из std
            std::cout << "std error: " << e.what() << "\n";
            throw;                            // переброс того же исключения
        }
    } catch (const std::out_of_range&) {
        std::cout << "outer got out_of_range\n";
    } catch (...) {
        std::cout << "something else\n";
    }
}

int main() {
    for (int i = 0; i <= 3; ++i) attempt(i);
}
```
```output
ok
config error at line 7: missing key
std error: index
outer got out_of_range
something else
```

**Пример 3. `noexcept` и рост `vector`: копирование или перемещение.**

```cpp run
#include <iostream>
#include <utility>
#include <vector>

struct Counters {
    inline static int copies = 0;
    inline static int moves = 0;
};

struct Safe : Counters {
    Safe() = default;
    Safe(const Safe&) { ++copies; }
    Safe(Safe&&) noexcept { ++moves; }
};

struct Risky : Counters {
    Risky() = default;
    Risky(const Risky&) { ++copies; }
    Risky(Risky&&) { ++moves; }  // без noexcept
};

template <class T>
void grow(const char* label) {
    std::vector<T> v;
    v.reserve(2);
    v.emplace_back();
    v.emplace_back();
    Counters::copies = Counters::moves = 0;
    v.emplace_back();  // ёмкость исчерпана: два элемента переезжают в новый блок
    std::cout << label << ": copies=" << Counters::copies << " moves=" << Counters::moves << "\n";
}

int main() {
    grow<Safe>("noexcept move");
    grow<Risky>("plain move");
    std::cout << noexcept(Safe(std::declval<Safe>())) << " " << noexcept(Risky(std::declval<Risky>())) << "\n";
}
```
```output
noexcept move: copies=0 moves=2
plain move: copies=2 moves=0
1 0
```

Если перемещение может бросить, `vector` предпочитает копирование: так при исключении в середине переезда исходные элементы остаются целыми. Поэтому конструктор перемещения нужно помечать `noexcept`, если он не бросает.

**Пример 4. `optional`, `from_chars` и коды ошибок.**

```cpp run
#include <charconv>
#include <iostream>
#include <optional>
#include <string>
#include <string_view>
#include <system_error>

std::optional<int> parseStrict(std::string_view s) {
    int v = 0;
    const char* end = s.data() + s.size();
    auto [ptr, ec] = std::from_chars(s.data(), end, v);
    if (ec != std::errc{} || ptr != end || s.empty()) return std::nullopt;
    return v;
}

int main() {
    for (std::string_view s : {"42", "-7", "12abc", "", "+5", "99999999999"}) {
        auto r = parseStrict(s);
        std::cout << "[" << s << "] -> " << (r ? std::to_string(*r) : std::string("none")) << "\n";
    }

    std::optional<int> missing;
    std::cout << missing.value_or(-1) << "\n";
    try {
        std::cout << missing.value() << "\n";
    } catch (const std::bad_optional_access&) {
        std::cout << "bad_optional_access\n";
    }

    std::cout << std::stoi("12abc") << "\n";  // stoi принимает префикс: 12abc читается как 12
}
```
```output
[42] -> 42
[-7] -> -7
[12abc] -> none
[] -> none
[+5] -> none
[99999999999] -> none
-1
bad_optional_access
12
```

Строка `"+5"` отвергнута: `from_chars` не принимает знак `+`. Старая функция `stoi` молча приняла бы `12abc`.

**Пример 5. `std::expected` (C++23, не запускается в среде курса).**

```cpp
#include <charconv>
#include <expected>
#include <iostream>
#include <string_view>
#include <system_error>

enum class ParseError { Empty, NotANumber, OutOfRange };

std::expected<int, ParseError> parse(std::string_view s) {
    if (s.empty()) return std::unexpected(ParseError::Empty);
    int v = 0;
    auto [p, ec] = std::from_chars(s.data(), s.data() + s.size(), v);
    if (ec == std::errc::result_out_of_range) return std::unexpected(ParseError::OutOfRange);
    if (ec != std::errc{} || p != s.data() + s.size()) return std::unexpected(ParseError::NotANumber);
    return v;
}

std::expected<int, ParseError> doubled(std::string_view s) {
    return parse(s).transform([](int v) { return v * 2; });   // ошибка проходит насквозь
}

int main() {
    for (std::string_view s : {"21", "", "12abc", "99999999999"}) {
        auto r = doubled(s);
        if (r) std::cout << *r << "\n";
        else   std::cout << "error " << static_cast<int>(r.error()) << "\n";
    }
}
```

Этот код собирается командой `clang++ -std=c++23` и печатает `42`, `error 0`, `error 1`, `error 2`. С `-std=c++20` компилятор сообщает, что в пространстве имён `std` нет шаблона `expected`.

**Пример 6. Ловушки (не запускать).**

```cpp
void bad1() {
    try { risky(); }
    catch (std::exception e) { throw e; }      // срезка: теряется производный тип; throw; было бы верно
}

struct Holder {
    ~Holder() noexcept(false) { throw std::runtime_error("from dtor"); }  // при раскрутке -> std::terminate
};

void bad2() noexcept {
    throw std::runtime_error("boom");          // обещали не бросать: вызывается std::terminate
}

int bad3(std::optional<int> o) { return *o; } // пустой optional: неопределённое поведение, не исключение
```

<!-- section: mistakes -->
## Типичные ошибки

* **Ловить по значению** (`catch (std::exception e)`) - срезка и потеря информации. Ловите `const T&`.
* **`throw e;` вместо `throw;`** в обработчике - копирует и срезает исключение.
* **Исключение из деструктора.** Во время раскрутки это `std::terminate`. Деструкторы не должны бросать.
* **Конструктор перемещения без `noexcept`.** `vector` при росте копирует, и вы теряете производительность.
* **`std::stoi` для проверки ввода.** Он принимает префикс (`12abc`) и пропускает пробелы; для строгого разбора применяйте `from_chars` и проверку конца.
* **`*opt` без проверки.** Это неопределённое поведение, а не исключение; `value()` бросает `bad_optional_access`.
* **`catch (...) {}`** - молчаливое проглатывание. Хотя бы залогируйте.
* **Исключения как управление потоком** для ожидаемых исходов (поиск, конец данных). Используйте `optional` или `expected`.
* **Использовать `expected` в `-std=c++20`.** Нужен `-std=c++23`; проверяйте версию стандарта, прежде чем полагаться на возможность.

<!-- section: summary -->
## Итоги

* Исключения - для редких ошибок, которые нужно пронести через слои; `optional`, `expected` и коды - для ожидаемых исходов.
* Ловите по `const&`, перебрасывайте через `throw;`, не бросайте из деструкторов.
* `noexcept` у перемещения и `swap` влияет на производительность контейнеров; нарушение обещания означает `std::terminate`.
* `from_chars` с проверкой конца - строгий разбор без исключений. `optional` - C++17, `expected` - C++23 (проверено компиляцией с `-std`).

Дальше: [неопределённое поведение и санитайзеры](lesson:cpp.fundamentals.undefined-behaviour).
Назад: [контейнеры и алгоритмы](lesson:cpp.fundamentals.stl-containers-algorithms).
Сравните: [ошибки в Rust](lesson:rust.fundamentals.error-handling), [исключения и ресурсы в C#](lesson:csharp.fundamentals.exceptions-and-resources), [try-with-resources в Java](lesson:java.fundamentals.exceptions-try-with-resources), [функции и ошибки в Go](lesson:go.fundamentals.functions-and-errors).
