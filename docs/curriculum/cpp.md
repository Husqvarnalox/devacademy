# Трек C++: структура

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Эталоны: [go.md](go.md), [javascript.md](javascript.md), ближайшие по устройству - [java.md](java.md), [csharp.md](csharp.md) и [kotlin.md](kotlin.md) (тоже компилируемые языки). Маршрут: `content/routes/cpp.yaml`.

```
cpp-from-zero (уровень 0) ──┐
                            ├─> cpp-first-steps (препроцессор, компоновщик, неопределённое поведение; значения, ссылки, RAII) ─> cpp-fundamentals
cpp-for-programmers (1) ────┘
   зачитывает уроки cpp-from-zero
cpp-skill-check: проверки компетенций
```

## Особенности C++, которые определили порядок

* **Как запускается программа.** Каждое упражнение - один файл `main.cpp` (`entryFile`). Приложение собирает все `.cpp`-файлы
  каталога одной командой `clang++` (или `g++`, если нет clang) с ключами `-std=c++23 -pthread -O2 -Wall -Wextra`, затем
  запускает получившийся `prog` (`CppRuntime` в `src/DevAcademy.Execution/Languages/Runtimes.cs`). Если компилятор не знает
  C++23, приложение собирает как C++20, поэтому from-zero не использует ничего, чего нет в C++20 (`std::expected`,
  `std::print` и т. п. - не используются). Предупреждения (`-Wall -Wextra`) видны ученику; уроки 02 и 04 учат читать их.
  Тексты сообщений в теории получены запуском Apple clang 21 на файле `main.cpp`; `g++` формулирует иначе, но называет то же
  место, и урок 02 говорит об этом прямо.
* **Три вещи неизбежны с первой строки: `#include <iostream>`, `int main()` и `std::cout`.** В уроке 01 каждая получает
  честное объяснение в одну строку: `#include` - «подключить готовую часть, которая умеет печатать», `int main()` - «с этого
  места начинается программа», `std::` - «имя из стандартной библиотеки». Подробности (как работает препроцессор, что значит
  `int` у `main` и код возврата) отложены: «подробно - в уроке 17» (функции) и в `cpp-first-steps` (препроцессор,
  компоновщик). Для упражнения понимать это не нужно: правки идут внутри `main`.
* **Всегда `std::`, никогда `using namespace std;`.** Причина объясняется один раз (урок 01, раздел «Точные правила»):
  в больших программах чужие имена сталкиваются с вашими, а префикс `std::` показывает, откуда имя. Ни одна заготовка курса не
  содержит `using namespace std;`.
* **Только современный C++ в основном пути:** `std::string`, `std::vector`, `std::map`, range-for, `const`, ссылки как
  параметры функций (`const std::string&`). Нет `new`/`delete`, C-массивов, `printf`/`scanf`, указателей, `char*`,
  `NULL`. Семантика значений, конструкторы/деструкторы и RAII - `cpp-first-steps` (урок 02); указатели и умные указатели -
  `cpp-fundamentals`.
* **Форматирование: `std::fixed << std::setprecision(2)`** (урок 10, `<iomanip>`). `std::format` работает не в каждой
  стандартной библиотеке (clang со старым libstdc++ и g++ 12 его не имеют), поэтому в курсе он не используется и упомянут
  только в разделе для программистов и в «Углублении». `std::cout` печатает дробные числа с точкой независимо от локали
  компьютера (потоки по умолчанию используют классическую локаль), поэтому ловушки локали, как в Java, здесь нет.
* **Ввод вводится рано (урок 08):** `std::cin >> x` - одна строка, без обёрток и преобразований, поэтому упражнения могут
  проверяться на разных данных. Чтение «пока есть данные» - `while (std::cin >> x)` (урок 14); чтение строки целиком и
  ловушка «`getline` после `>>`» - урок 21. До урока 08 stdin пуст, и проверяется фиксированный вывод.
* **Неопределённое поведение (UB) - тема не «потом», а с первых уроков:** неинициализированная переменная (урок 04), деление
  целых и переполнение `int` (урок 06), индекс за границей строки и `.at()` (урок 07), `v[i]` и `map[]` (уроки 19-20),
  `switch` без `break` (урок 13), цикл, который не заканчивается (урок 14). В каждом уроке показано, что делать: инициализировать,
  проверять границы, использовать `.at()`. Объяснение «почему стандарт ничего не обещает» и санитайзеры - `cpp-first-steps`
  (урок 01) и `cpp-fundamentals` (`undefined-behaviour`).
* **Размер строки - в байтах.** `std::string::size()` считает байты, а не буквы: у русского слова из шести букв размер 12
  (кодировка UTF-8). Индексация, `size()` и `substr` в упражнениях используют только латиницу и цифры; русский текст
  встречается в строках, которые только печатаются или читаются целиком (`getline`). Причина объясняется в уроке 07.
* **`std::string::size()` возвращает беззнаковое число.** `s.size() - 1` для пустой строки даёт огромное число; сравнение
  `int` с `size()` вызывает предупреждение `-Wsign-compare`. Это показано как реальное сообщение компилятора (урок 07 и 11),
  а счётчики циклов по строкам и векторам пишутся как `for (std::size_t i = 0; ...)` (урок 15) или range-for (урок 19).
* **Деление целых и переполнение.** `7 / 2` равно `3`; `int` держит примерно ±2,1 млрд, переполнение знакового числа - UB
  (урок 06). Для сумм и произведений вводится `long long`; явные преобразования `static_cast<double>` - урок 09.
* **Строки сравниваются `==`** (в отличие от Java): `std::string` сравнивается по содержимому. Ловушки с `char*` во
  from-zero не возникают, потому что C-строк нет.
* **Функции - обычные функции вне классов** (урок 17). С урока 17 заготовка содержит готовый `main`, который читает
  ввод и вызывает функции ученика, поэтому тесты меняют данные. Порядок объявления («функция должна быть объявлена
  раньше, чем вызвана») объясняется там же: функции идут над `main`. Параметры-ссылки (`const std::string&`, `int&`) - урок 18
  как продолжение темы функций.
* **Коллекции:** `std::vector<T>` (урок 19) и `std::map<K, V>` (урок 20). Угловые скобки объясняются как «тип элементов».
  Обход - range-for; `auto` вводится в уроке 20 только для пары «ключ-значение» (`const auto& [name, count] : m`). Итераторы,
  `<algorithm>`, `unordered_map`, `set` - `cpp-fundamentals` (`stl-containers-algorithms`).
* **Исключения (урок 22):** `try/catch`, `throw std::invalid_argument(...)`, `std::stoi`, который бросает исключение на
  плохой строке, и `.at()`, который бросает `std::out_of_range`. Иерархия, `noexcept`, `std::optional`/`std::expected`,
  стоимость исключений - `cpp-fundamentals` (`error-handling`).
* **Первая структура (урок 23) - мост:** `struct` с полями и простой функцией-членом. Конструкторы, деструкторы,
  `class`, `public/private`, наследование - `cpp-first-steps` (урок 02) и `cpp-fundamentals`.
* **Что отложено** в `cpp-first-steps` / `cpp-fundamentals`: стадии трансляции, заголовочные файлы и компоновка нескольких
  `.cpp`, указатели, `new`/`delete`, умные указатели, перемещение, шаблоны, STL-алгоритмы, `constexpr`, потоки. Во
  from-zero это встречается только в разделах «Для тех, кто уже программирует» и «Углубление».

## cpp-from-zero (уровень 0)

Id `cpp.from-zero.<slug>`; каталог `NN-<slug>`; файл решения - `main.cpp`. Все упражнения - `io`.

| # | slug | Тема | introduces | Проверка |
|---|---|---|---|---|
| **Блок 0. Знакомство** |||||
| 01 | first-program | что такое программа и компилятор; `#include`, `int main`, `std::cout`, префикс `std::` | cpp.program, cpp.compiler, cpp.include, cpp.main, cpp.cout, cpp.string-literal, cpp.std-prefix | io, A |
| 02 | compiler-errors | как читать сообщение компилятора: файл:строка:столбец, `error`/`warning`, `^`, каскад ошибок | cpp.compile-error, cpp.error-location, cpp.warning | io, B (исправить) |
| 03 | output | цепочка `<<`, `"\n"` и `std::endl`, `\"`, `\\`, `\t`, комментарии | cpp.output-chain, cpp.newline, cpp.escape-sequence, cpp.comment | io, B |
| **Блок 1. Значения** |||||
| 04 | variables | объявление, присваивание, `int`, `std::string`, инициализация, неинициализированная переменная (UB) | cpp.variable, cpp.assignment, cpp.int, cpp.string, cpp.initialization, cpp.undefined-behavior | io, B |
| 05 | basic-types | `double`, `bool` и `std::boolalpha`, `char`, `const`, литералы | cpp.type, cpp.double, cpp.bool, cpp.char, cpp.const | io, B |
| 06 | arithmetic | `+ - * / %`, целочисленное деление, `+=` и `++`, диапазон `int`, переполнение (UB), `long long` | cpp.arithmetic, cpp.integer-division, cpp.remainder, cpp.compound-assignment, cpp.integer-overflow, cpp.long-long | io, B |
| 07 | strings | `size()`, `[]`, `.at()`, склейка `+`, `empty()`, индексы с нуля, размер в байтах | cpp.string-size, cpp.string-index, cpp.at, cpp.string-concat | io, B |
| 08 | reading-input | `std::cin >> x` для `int`, `double`, слова; пробелы и переводы строк | cpp.cin, cpp.whitespace-separated-input | io, C |
| 09 | type-conversion | `static_cast`, усечение при `double` -> `int`, `std::stoi`/`std::stod`, `std::to_string` | cpp.static-cast, cpp.truncation, cpp.stoi, cpp.to-string | io, B |
| 10 | formatted-output | `std::fixed` + `std::setprecision`, `std::setw`, `std::left`/`std::right`, `<iomanip>` | cpp.fixed-setprecision, cpp.setw, cpp.iomanip | io, B |
| 11 | comparison-and-logic | `== != < > <= >=`, `&& \|\| !`, приоритет, короткое вычисление, `-Wsign-compare` | cpp.comparison, cpp.logical-operators, cpp.boolean-expression, cpp.short-circuit | io, B |
| **Блок 2. Управление** |||||
| 12 | if-else | `if / else if / else`, блок `{}`, `=` против `==` | cpp.if, cpp.else, cpp.block | io, C |
| 13 | switch | `switch` по `int` и `char`, `case`, `break`, `default`, «проваливание» | cpp.switch, cpp.case-break | io, C |
| 14 | while-loop | `while`, счётчик, накопление суммы, чтение до конца ввода `while (std::cin >> x)` | cpp.while, cpp.loop-condition, cpp.read-until-eof | io, C |
| 15 | for-loop | `for (int i = 0; ...)`, шаг, обратный отсчёт, `std::size_t` | cpp.for, cpp.loop-variable | io, C |
| 16 | loop-control | `break`, `continue`, вложенные циклы | cpp.break, cpp.continue, cpp.nested-loops | io, C |
| **Блок 3. Функции и коллекции** |||||
| 17 | functions | функция: параметры, `return`, `void`, область видимости, порядок объявления | cpp.function, cpp.parameter, cpp.return, cpp.void, cpp.scope, cpp.declaration-order | io (готовый `main`), C |
| 18 | references | передача по значению, `const T&`, `T&` как выходной параметр | cpp.pass-by-value, cpp.reference, cpp.const-reference | io (готовый `main`), C |
| 19 | vector | `std::vector<T>`: `push_back`, `size`, `[]`, `.at()`, range-for, выход за границы | cpp.vector, cpp.push-back, cpp.range-for, cpp.vector-index | io, C |
| 20 | map | `std::map<K, V>`: `m[key]`, `count`, `at`, обход, пара `const auto& [k, v]`, ловушка `m[key]` | cpp.map, cpp.key-value, cpp.auto, cpp.map-index | io, C |
| 21 | text-lines | `std::getline`, ловушка `>>` + `getline`, сравнение строк `==`, `find`, `substr`, разбор `имя;балл` | cpp.getline, cpp.string-compare, cpp.find, cpp.substr, cpp.npos | io, C |
| **Блок 4. Надёжность и мост к first-steps** |||||
| 22 | exceptions | `try/catch`, `throw std::invalid_argument`, `std::stoi` на плохой строке, `.at()` и `out_of_range` | cpp.exception, cpp.try-catch, cpp.throw | io, C |
| 23 | first-struct | первая структура: поля, функция-член, объект | cpp.struct, cpp.field, cpp.member-function, cpp.object | io, C |
| 24 | mini-project | мини-проект: ведомость оценок из строк `имя;балл` (плохие строки считаются и пропускаются) | - | io, D |

Правила для последующих уроков (12-24):

* Уроки 12-16 используют числа и символы в условиях; сравнение строк - с урока 21 (до него строки сравнивают только `==` в
  примере урока 11 и в `switch`-свободных проверках слова из `cin`).
* Урок 14 вводит чтение «пока есть данные» (`while (std::cin >> x)`), чтобы упражнения принимали ввод разной длины.
* С урока 17 заготовка содержит готовый `main`; ученик пишет функции над ним (порядок объявления!).
* Урок 19 использует range-for и `.at()` в примерах выхода за границы (`std::out_of_range` ловит урок 22).
* Урок 21 вводит `getline`; разбор строки `имя;балл` делается через `find` и `substr`, без `istringstream`.
* Урок 23 - мост: `struct Student { std::string name; int score; int total() ...}` в том же `main.cpp`; следующий курс
  начинается с препроцессора, компоновщика и ключей компилятора.
* Урок 24 использует `std::getline`, `find`/`substr`, `std::stoi` в `try/catch`, `std::map`/`std::vector` и структуру из урока 23.

## cpp-for-programmers (уровень 1)

Id `cpp.programmers.<slug>`; упражнение - io с готовым `main`, квиз на ловушки C++ (неинициализированные переменные, UB
при переполнении `int`, целочисленное деление, `v[i]` против `.at()`, `m[key]` вставляет элемент, `size() - 1` у пустой
строки, `getline` после `>>`, `size()` в байтах, `std::endl` и сброс буфера, сужающее преобразование в `{}`).

| # | slug | assessmentFor (`cpp.from-zero.*`) |
|---|---|---|
| 01 | syntax-types-and-control-flow | 01-16 |
| 02 | functions-collections-and-strings | 17-21 |
| 03 | exceptions-and-structs | 22-23 |

Урок 24 (мини-проект) в зачёт не входит.

## cpp-skill-check

Id `cpp.check.<slug>`; урок-проверка = упражнение (io) + квиз на чтение кода.

| Компетенция | slug | assessmentFor |
|---|---|---|
| Основы: значения, ввод, условия, циклы | basics | from-zero 01-16 |
| Функции, vector, map, строки | collections | from-zero 17-21 |
| Ошибки и ввод | errors-input | from-zero 08, 09, 22 |
| Значения, ссылки и RAII | values-raii | cpp.first-steps.value-semantics-references-and-raii |
| Умные указатели | smart-pointers | cpp.fundamentals.raii-smart-pointers |
| Классы и виртуальные функции | classes | cpp.fundamentals.classes-inheritance-virtual |
| Контейнеры STL и алгоритмы | stl | cpp.fundamentals.stl-containers-algorithms |
| Исключения и неопределённое поведение | errors-ub | cpp.fundamentals.error-handling, cpp.fundamentals.undefined-behaviour |

(Точный состав проверок уровня first-steps/fundamentals уточняется в фазе C по `lesson.yaml` этих уроков.)

## Связи

* `cpp-first-steps`: `requires: [cpp-from-zero]`; урок 01 (`cpp.first-steps.how-cpp-runs`) зависит от
  `cpp.from-zero.first-struct` (урок 23). Урок 24 (мини-проект) - необязательное завершение; его `next` - урок
  `cpp.first-steps.how-cpp-runs`. Упражнение урока 01 first-steps (сумма, минимум, максимум и среднее из `std::cin`) во
  from-zero уже почти достигнуто (уроки 08, 10, 14), поэтому цели и формулировки первого урока first-steps нужно сместить к
  препроцессору, компоновщику, ключам `-std`/`-O2`/`-Wall` и неопределённому поведению; тексты про `std::cin`,
  `setprecision` и `long long` оставить как повторение, а не как новое.
* `cpp-fundamentals`: `requires: [cpp-first-steps]` (как сейчас).
* `cpp-for-programmers`: последний урок ведёт в `cpp.first-steps.how-cpp-runs`.
