<!-- section: simple -->
## Зачем этот урок

Функции и коллекции в C++ выглядят как в любом языке: `std::vector` - список, `std::map` - словарь, `std::string` - строка. Отличие в том, что всё это **значения**. Переменная типа `std::vector<int>` хранит сам вектор, а не ссылку на него: присваивание и передача в функцию копируют все элементы, пока вы не попросите иначе (`&`). Вторая особенность - у индекса нет проверки: `v[10]` у вектора из трёх элементов - неопределённое поведение, а `std::map::operator[]` для отсутствующего ключа **вставляет** элемент. Успешное прохождение (упражнение и квиз) засчитывает уроки 17-21 курса «C++ с нуля».

## Главная мысль

В C++ вы выбираете способ передачи сами: по значению (копия), по `const T&` (читать без копии) или по `T&` (изменять оригинал). Для строк и коллекций, которые функция только читает, выбирают `const&`; для результата возвращают значение: копирования не будет, компилятор перемещает или строит объект на месте. Для доступа по индексу используют `.at()` там, где граница не гарантирована, а для словаря - `count`, `contains` или `at`, если вставка не нужна.

<!-- section: examples -->
## Пример 1. Значения и ссылки

```cpp run
#include <iostream>
#include <string>
#include <vector>

int sumFirst(std::vector<int> v) {
    int total = 0;
    while (v.size() > 2) {
        total += v.back();
        v.pop_back();
    }
    return total;
}

void addTen(std::vector<int>& v) {
    v.push_back(10);
}

std::size_t lengthOf(const std::string& text) {
    return text.size();
}

std::string shout(std::string word) {
    word += "!";
    return word;
}

int main() {
    std::vector<int> numbers = {1, 2, 3, 4};
    std::cout << sumFirst(numbers) << " " << numbers.size() << "\n";
    addTen(numbers);
    std::cout << numbers.size() << "\n";

    std::string word = "hi";
    std::cout << shout(word) << " " << word << " " << lengthOf(word) << "\n";

    std::vector<int> copy = numbers;
    copy[0] = 99;
    std::cout << numbers[0] << " " << copy[0] << "\n";

    std::vector<int> a(3);
    std::vector<int> b{3};
    std::cout << a.size() << " " << b.size() << " " << a[0] << "\n";

    int doubled = 0;
    for (int x : numbers) {
        x *= 2;
        doubled += x;
    }
    std::cout << numbers[0] << " " << doubled << "\n";
    for (int& x : numbers) {
        x *= 2;
    }
    std::cout << numbers[0] << "\n";
}
```
```output
7 4
5
hi! hi 2
1 99
3 1 0
1 40
2
```

Разбор. Функция `sumFirst` получила вектор **копией**: она вынимает элементы из копии, и `numbers` в `main` остаётся из четырёх чисел. Функция `addTen` принимает `std::vector<int>&` и меняет оригинал. Параметр `const std::string&` читает строку без копии и не позволяет её изменить, а `shout` получает копию и возвращает изменённую. Присваивание `copy = numbers` тоже копирует: после `copy[0] = 99` оригинал не изменился (в Java, Python и JavaScript обе переменные указывали бы на один объект). Запись `std::vector<int> a(3)` создаёт три нуля, а `b{3}` - один элемент `3`: круглые скобки задают размер, фигурные - содержимое. Цикл `for (int x : numbers)` работает с копиями элементов и не меняет вектор, а `for (int& x : numbers)` - с самими элементами.

**Попробуйте.** Замените `std::vector<int> v` в `sumFirst` на `const std::vector<int>& v`. Что скажет компилятор про `v.pop_back()`?

## Пример 2. Индексы и словарь

```cpp run
#include <iostream>
#include <map>
#include <stdexcept>
#include <string>
#include <vector>

int main() {
    std::vector<int> v = {10, 20, 30};
    try {
        std::cout << v.at(5) << "\n";
    } catch (const std::out_of_range&) {
        std::cout << "at(5): out_of_range\n";
    }
    std::cout << v.size() - 1 << " " << v.back() << "\n";

    std::map<std::string, int> stock;
    stock["apple"] = 3;
    std::cout << stock["pear"] << "\n";
    std::cout << stock.size() << "\n";
    std::cout << stock.count("kiwi") << " " << stock.size() << "\n";
    std::cout << stock.contains("apple") << "\n";
    try {
        std::cout << stock.at("kiwi") << "\n";
    } catch (const std::out_of_range&) {
        std::cout << "at(kiwi): out_of_range\n";
    }
    stock["zebra"] = 1;
    stock["banana"] += 5;
    for (const auto& [name, count] : stock) {
        std::cout << name << "=" << count << "\n";
    }

    std::vector<std::vector<int>> grid(2, std::vector<int>(3, 7));
    std::cout << grid.size() << "x" << grid[0].size() << " " << grid[1][2] << "\n";
}
```
```output
at(5): out_of_range
2 30
0
2
0 2
1
at(kiwi): out_of_range
apple=3
banana=5
pear=0
zebra=1
2x3 7
```

Разбор. `v.at(5)` бросает `std::out_of_range`, который можно перехватить; `v[5]` ничего не проверяет и читает чужую память - UB. Тип сообщения исключения мы не проверяем: тексты зависят от стандартной библиотеки. Поиск `stock["pear"]` в словаре **создал** запись со значением `0`, и размер вырос с одного до двух; `count` и `contains` (C++20) ничего не вставляют, `at` бросает исключение для отсутствующего ключа. `stock["banana"] += 5` работает потому, что новая запись получает `0`. `std::map` хранит ключи упорядоченно, поэтому обход идёт по алфавиту. `auto` в `const auto& [name, count]` выводит тип пары «ключ - значение» и распаковывает её на два имени. Двумерная таблица - вектор векторов: `grid(2, std::vector<int>(3, 7))` - две строки по три семёрки.

## Пример 3. Строки

```cpp run
#include <iostream>
#include <sstream>
#include <string>

int main() {
    std::istringstream input("5\nhello world\n");
    int n = 0;
    input >> n;
    std::string line;
    std::getline(input, line);
    std::cout << "[" << line << "]\n";
    std::getline(input, line);
    std::cout << "[" << line << "]\n";

    std::string s = "name=Anna;score=90";
    std::size_t eq = s.find('=');
    std::size_t none = s.find('#');
    std::cout << eq << " " << (none == std::string::npos) << "\n";
    std::cout << s.substr(0, eq) << "|" << s.substr(eq + 1, 4) << "|" << s.substr(s.size()) << "|\n";

    std::string t = s;
    t[0] = 'N';
    std::cout << s << " " << t.substr(0, 4) << "\n";

    std::string word = "abc";
    word.insert(1, "XY");
    word.erase(0, 1);
    std::cout << word << " " << word.find("Y") << "\n";
    std::cout << (std::string("apple") < std::string("banana")) << "\n";
    std::cout << std::stoi("42") + 1 << " " << std::stod("2.5") * 2 << "\n";
}
```
```output
[]
[hello world]
4 1
name|Anna||
name=Anna;score=90 Name
XYbc 1
1
43 5
```

Разбор. После `input >> n` в потоке остался перевод строки: следующий `getline` прочитал пустую строку, и только второй - настоящую. Это та же ловушка, что при смешивании `std::cin >> x` и `std::getline(std::cin, line)`: после `>>` вызывают `std::cin.ignore(...)` или читают всё построчно. Метод `find` возвращает `std::string::npos` (наибольшее `std::size_t`), если подстроки нет, поэтому результат сравнивают с `npos`, а не с `-1`. `substr(pos, len)` бросает `std::out_of_range`, если `pos` больше длины; при `pos` равном длине возвращается пустая строка. Копия строки независима: изменение `t[0]` не затронуло `s`. Строки сравниваются по байтам лексикографически, `std::stoi` и `std::stod` разбирают числа (строгий разбор - в следующем уроке).

<!-- section: precise -->
## Точные правила

* Функция должна быть **объявлена до вызова**: определение выше `main` или отдельное объявление (прототип) `int twice(int x);`. Вызов необъявленного имени - ошибка компиляции.
* Параметр без `&` - копия: для `std::vector` и `std::string` копируются все элементы. Читающая функция принимает `const T&`; изменяющая - `T&`. Для `int`, `double`, `char`, `bool` копия дешевле ссылки, и `const&` не нужен.
* Результат возвращают по значению. Возврат ссылки или указателя на локальную переменную - UB (висячая ссылка): локальный объект уничтожается при выходе из функции.
* Константная ссылка может связываться с временным значением (`lengthOf("abc")`), неконстантная - нет.
* Круглые и фигурные скобки при создании контейнера различаются: `std::vector<int> v(3)` - три нуля, `std::vector<int> v{3}` - один элемент. Для `std::vector<int> v(3, 7)` - три семёрки.
* `v[i]` без проверки границ - UB при `i >= size()`; `v.at(i)` бросает `std::out_of_range`. `v.front()`, `v.back()` и `pop_back()` для пустого вектора - UB.
* Изменять вектор во время обхода range-for (`push_back`, `erase`) нельзя: итераторы становятся недействительными.
* `std::map<K, V>::operator[]` при отсутствии ключа вставляет значение по умолчанию (`0` для чисел, пустая строка для `std::string`); оператор неприменим к `const` словарю. Для проверки без вставки: `count`, `contains` (C++20), `find`, для чтения с ошибкой - `at`.
* `std::map` упорядочен по ключу; `std::unordered_map` - нет, порядок обхода не задан и от запуска к запуску не гарантируется.
* `s.find(x)` возвращает `std::string::npos`, если не нашёл. `s.substr(pos, len)` при `pos > size()` бросает исключение; `len` можно не указывать.
* `std::to_string(double)` печатает шесть знаков после запятой (`1.500000`); ему нельзя задать формат.

<!-- section: mistakes -->
## Типичные ошибки

**1. Использование функции до её объявления.**

```
main.cpp:15:18: error: use of undeclared identifier 'twice'
```

Перенесите определение выше `main` или добавьте прототип.

**2. Изменение через `const&`.** Параметр `const std::string& s` и строка `s += "!";`:

```
main.cpp:10:7: error: no viable overloaded '+='
```

Если функция должна менять строку, параметр - `std::string&`; если нужен только результат, принимайте копию и возвращайте её.

**3. Ссылка на локальную переменную.**

```cpp
const std::string& makeName() {
    std::string name = "Anna";
    return name;
}
```

```
main.cpp:6:12: warning: reference to stack memory associated with local variable 'name' returned [-Wreturn-stack-address]
```

Это только предупреждение: программа собирается, а результат при запуске непредсказуем. Возвращайте `std::string` по значению.

**4. Присваивание строки числу.** `int x = s;`:

```
main.cpp:7:9: error: no viable conversion from 'std::string' (aka 'basic_string<char>') to 'int'
```

Преобразование идёт явно: `std::stoi(s)`.

**5. Знаковый счётчик и `size()`.** Цикл `for (int i = 0; i < v.size(); i++)` компилируется с предупреждением:

```
main.cpp:6:23: warning: comparison of integers of different signs: 'int' and 'size_type' (aka 'unsigned long') [-Wsign-compare]
```

Используйте `std::size_t i` или range-for.

**6. Поиск через `m[key]`.** Проверка `if (m["x"] == 0)` вставляет `"x"` в словарь. Для проверки наличия есть `count` и `contains`.

**7. Вычитание из `size()`.** Выражение `i < v.size() - 1` для пустого вектора истинно при любом `i`, и цикл читает память за границами. Пишите `i + 1 < v.size()`.

**8. Передача большого вектора по значению.** Программа работает, но каждый вызов копирует все элементы. Ищите это там, где функция только читает.

**9. Размер русской строки.** `"привет".size()` равно `12`: это байты UTF-8. Разбирайте такой текст построчно и целыми словами, не по индексам.

<!-- section: summary -->
## Итог

Вы знаете главные отличия C++ в функциях и коллекциях: аргументы копируются, пока не указан `&`, `const&` читает без копии, `[]` не проверяет границы, а `map[]` вставляет, `find` возвращает `npos`, а размер строки считает байты. Следующий урок - исключения и структуры: [Исключения и структуры](lesson:cpp.programmers.exceptions-and-structs). Для подробностей: [Передача по значению и по ссылке](lesson:cpp.from-zero.references), [Контейнер vector](lesson:cpp.from-zero.vector), [Контейнер map](lesson:cpp.from-zero.map).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Объекты - значения, как `struct` в Go и C#, а не ссылки, как в Java, Python и JavaScript. `b = a` для `std::vector` копирует всё содержимое; общий объект получается только через ссылку или указатель.
* `const std::string&` - привычный способ передать строку без копирования; в Java и C# строки неизменяемы, в Go строка - дешёвый заголовок, поэтому копирование там не обсуждают.
* `std::vector` - аналог `ArrayList`, `List<T>`, `list` и слайса Go; `push_back` - `append`. Индексация без проверки, в отличие от всех перечисленных; безопасная версия - `.at()`.
* `std::map` - сбалансированное дерево, упорядочено по ключу (как `TreeMap`); аналог `HashMap` - `std::unordered_map`. Чтение отсутствующего ключа в Go возвращает нулевое значение и ничего не вставляет, в Python бросает `KeyError`, `get` в Java возвращает `null`; `m[key]` в C++ **вставляет** запись.
* `std::string` изменяема (как `StringBuilder`), `==` сравнивает содержимое, `+` склеивает, но `"a" + "b"` для литералов не компилируется.
* Возврат локального значения не копирует: компилятор строит результат на месте или перемещает; `return std::move(local)` обычно вредит.
* Поиск подстроки возвращает `npos`, а не `-1`, как `indexOf` в Java и JavaScript и `find` в Python. Сравнивать нужно с `std::string::npos`.
* Нет встроенного `split`, `join`, `trim`: для разбора используют `find` и `substr`, `std::istringstream` или алгоритмы.
