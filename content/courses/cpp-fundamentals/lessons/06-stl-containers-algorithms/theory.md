<!-- section: simple -->
## Контейнеры, итераторы и алгоритмы: три части одной библиотеки

Стандартная библиотека шаблонов (STL) строится на разделении:

* **Контейнеры** хранят элементы: `vector` (массив с ростом), `list` (двусвязный список), `map` и `set` (упорядоченные деревья), `unordered_map` и `unordered_set` (хеш-таблицы), `string`.
* **Итераторы** - «обобщённые указатели» на элементы контейнера; пара `begin()`/`end()` задаёт диапазон.
* **Алгоритмы** (`sort`, `find_if`, `accumulate`, `transform`, `lower_bound`) работают с диапазонами итераторов и не знают, какой именно контейнер за ними стоит. С C++20 к ним добавлены **диапазоны (ranges)**: алгоритмы принимают контейнер целиком, а `views` строят ленивые конвейеры `v | filter(...) | transform(...)`.

Выбор контейнера определяет скорость и «подводные камни»:

* по умолчанию - `std::vector`: элементы лежат подряд, быстрый обход и доступ по индексу;
* нужен поиск по ключу: `unordered_map` быстрее в среднем, `map` хранит ключи упорядоченными;
* `std::string` владеет символами, `std::string_view` - лёгкий **невладеющий** взгляд на чужую строку, и он не должен пережить эту строку.

Главная ловушка контейнеров - **инвалидация итераторов**: операция изменила контейнер, и ранее полученные итераторы, указатели и ссылки на элементы перестали быть действительными. Использовать их дальше - неопределённое поведение.

<!-- section: precise -->
## Точные правила

**`std::vector`.** Элементы лежат в одном непрерывном блоке; `size()` - сколько элементов, `capacity()` - сколько влезет без перевыделения. `push_back` при нехватке места выделяет новый блок (коэффициент роста стандартом не задан: в libc++ и libstdc++ это 2, в MSVC 1,5), переносит элементы (перемещением, если конструктор перемещения `noexcept`, иначе копированием; см. [урок про перемещение](lesson:cpp.fundamentals.references-pointers-move)) и освобождает старый. Амортизированная стоимость `push_back` - `O(1)`. `reserve(n)` заранее выделяет память и избегает лишних перевыделений.

**Инвалидация итераторов (кратко).**

| Контейнер | Операция | Что инвалидируется |
|---|---|---|
| `vector` | `push_back`, `insert`, `emplace` | при перевыделении - все итераторы, указатели и ссылки; иначе - с позиции вставки и `end()` |
| `vector` | `erase` | элементы с позиции удаления и `end()` |
| `list` | вставка, `splice` | ничего; при `erase` - только удалённый элемент |
| `map`, `set` | вставка | ничего; при `erase` - только удалённый |
| `unordered_map` | вставка с перехешированием | итераторы (но указатели и ссылки на элементы остаются действительными) |

Точные правила для каждой операции смотрите в документации контейнера.

**Идиомы удаления.** `v.erase(std::remove_if(v.begin(), v.end(), pred), v.end())` («стереть-удалить»): `remove_if` лишь сдвигает оставляемые элементы вперёд, а `erase` отсекает хвост. В C++20 это одна строка: `std::erase_if(v, pred)` (в `-std=c++17` не компилируется). При удалении в цикле используйте возвращаемое значение: `it = v.erase(it)`.

**`map` и `unordered_map`.**

* `std::map` - сбалансированное дерево: операции `O(log n)`, ключи упорядочены, есть `lower_bound`, `upper_bound`, диапазоны; ключи должны сравниваться через `<`.
* `std::unordered_map` - хеш-таблица: поиск, вставка, удаление в среднем `O(1)`, в худшем случае `O(n)`; порядок обхода **не определён** и может отличаться между реализациями и запусками, поэтому не строите на нём вывод. Ключу нужен `std::hash` и `==`. При росте таблицы происходит перехеширование.
* `m[key]` **вставляет** значение по умолчанию, если ключа нет, поэтому для проверки наличия используйте `find` или `contains` (C++20, в `-std=c++17` не компилируется). `try_emplace` (C++17) не трогает существующее значение.

**`std::string` и `std::string_view`.** Строка владеет буфером. `string_view` - пара «указатель, длина» без владения: копируется за `O(1)`, `substr` не копирует символы, но **не гарантирует завершающий нулевой символ** (передавать `data()` в функцию C нельзя, если не уверены). Опасность - время жизни: `string_view` на временную строку, на строку, которая перераспределилась (`push_back` в исходной), или на уничтоженную висит. Параметр функции типа `std::string_view` принимает и `std::string`, и литерал без копирования; хранить его в поле класса следует только если владелец гарантированно живёт дольше.

**Алгоритмы.** Требуют корректных диапазонов и предикатов. Для `sort` компаратор обязан задавать **строгое слабое упорядочение**: `comp(a, a)` должно быть ложью. Компаратор `<=` нарушает это и вызывает неопределённое поведение (возможен выход за границы массива). `stable_sort` сохраняет порядок равных элементов, `sort` - нет. Бинарный поиск (`lower_bound`, `binary_search`) требует отсортированного диапазона. `std::accumulate` складывает в тип начального значения (см. [урок про шаблоны](lesson:cpp.fundamentals.templates-concepts-constexpr)).

**Диапазоны (C++20).** `std::ranges::sort(v)` принимает контейнер; `views::filter`, `views::transform`, `views::take`, `views::reverse` строят **ленивые** представления, которые вычисляются при обходе и ничего не копируют. Представление не владеет данными: если исходный контейнер уничтожен или изменён, представление висит. В `-std=c++17` заголовок `<ranges>` недоступен.

**Сложность по умолчанию.** `vector`: доступ `O(1)`, вставка в конец амортизированно `O(1)`, вставка в середину `O(n)`; `list`: вставка по итератору `O(1)`, доступ по индексу отсутствует; `map`: `O(log n)`; `unordered_map`: в среднем `O(1)`. Из-за кэшей `vector` часто быстрее `list` даже для вставок в середину при небольших размерах; измеряйте, а не гадайте.

<!-- section: examples -->
## Примеры

**Пример 1. Вектор: ёмкость, перевыделение и адрес данных.**

```cpp run
#include <cstdint>
#include <iostream>
#include <vector>

int main() {
    std::vector<int> v;
    v.reserve(100);
    auto before = reinterpret_cast<std::uintptr_t>(v.data());
    for (int i = 0; i < 100; ++i) v.push_back(i);  // в пределах ёмкости: буфер не двигается
    auto afterFill = reinterpret_cast<std::uintptr_t>(v.data());
    std::cout << (before == afterFill) << " " << v.size() << " " << (v.capacity() >= 100) << "\n";

    v.push_back(100);  // ёмкость превышена: новый буфер, старые итераторы и ссылки недействительны
    auto afterGrow = reinterpret_cast<std::uintptr_t>(v.data());
    std::cout << (afterFill != afterGrow) << " " << (v.capacity() > 100) << "\n";

    v.shrink_to_fit();
    v.clear();
    std::cout << v.size() << "\n";
}
```
```output
1 100 1
1 1
0
```

Из-за перевыделения нельзя держать ссылку `int& first = v[0];` и затем вызывать `push_back`: после роста она указывает в освобождённый буфер.

**Пример 2. Безопасное удаление элементов.**

```cpp run
#include <algorithm>
#include <iostream>
#include <vector>

void print(const std::vector<int>& v) {
    for (int x : v) std::cout << x << " ";
    std::cout << "\n";
}

int main() {
    std::vector<int> a{1, 2, 3, 4, 5, 6, 7, 8};
    for (auto it = a.begin(); it != a.end();) {  // удаление в цикле: берём итератор, который вернул erase
        if (*it % 3 == 0) it = a.erase(it);
        else ++it;
    }
    print(a);

    std::vector<int> b{1, 2, 3, 4, 5, 6, 7, 8};
    b.erase(std::remove_if(b.begin(), b.end(), [](int x) { return x % 2 == 0; }), b.end());
    print(b);

    std::vector<int> c{1, 2, 3, 4, 5, 6, 7, 8};
    std::erase_if(c, [](int x) { return x > 5; });  // C++20
    print(c);
}
```
```output
1 2 4 5 7 8 
1 3 5 7 
1 2 3 4 5 
```

**Пример 3. `map` упорядочен, `operator[]` вставляет.**

```cpp run
#include <iostream>
#include <map>
#include <string>
#include <unordered_map>

int main() {
    std::map<std::string, int> ages{{"carol", 41}, {"alice", 30}, {"bob", 25}};
    for (const auto& [name, age] : ages) std::cout << name << "=" << age << " ";
    std::cout << "\n";

    auto it = ages.lower_bound("b");  // первый ключ, не меньший "b"
    std::cout << it->first << "\n";

    std::cout << ages.size() << " ";
    int missing = ages["zoe"];  // вставил zoe=0!
    std::cout << missing << " " << ages.size() << " " << ages.contains("zoe") << "\n";

    std::unordered_map<std::string, int> counts;
    for (const char* w : {"a", "b", "a", "c", "a", "b"}) ++counts[w];
    std::cout << counts.size() << " " << counts["a"] << " " << counts.at("b") << "\n";
}
```
```output
alice=30 bob=25 carol=41 
bob
3 0 4 1
3 3 2
```

Порядок обхода `unordered_map` здесь не печатается: он не определён стандартом.

**Пример 4. `string_view`: дешёвый взгляд и срезы без копий.**

```cpp run
#include <iostream>
#include <string>
#include <string_view>
#include <vector>

std::vector<std::string_view> splitBySpace(std::string_view text) {
    std::vector<std::string_view> parts;
    while (!text.empty()) {
        std::size_t pos = text.find(' ');
        parts.push_back(text.substr(0, pos));  // ни одного копирования символов
        if (pos == std::string_view::npos) break;
        text.remove_prefix(pos + 1);
    }
    return parts;
}

int main() {
    std::string line = "read the manual carefully";  // владелец жив, пока живы views
    std::vector<std::string_view> words = splitBySpace(line);
    for (std::string_view w : words) std::cout << "[" << w << "]";
    std::cout << " " << words.size() << "\n";

    std::string_view first = words[0];
    std::cout << (first.data() == line.data()) << " " << first.size() << "\n";  // указывает внутрь line
}
```
```output
[read][the][manual][carefully] 4
1 4
```

Здесь `line` живёт дольше всех `string_view`, поэтому всё безопасно. Опасный вариант - ниже, в примере 7.

**Пример 5. Алгоритмы: сортировка, поиск, преобразование.**

```cpp run
#include <algorithm>
#include <iostream>
#include <numeric>
#include <string>
#include <vector>

struct Person {
    std::string name;
    int age;
};

int main() {
    std::vector<Person> people{{"Dina", 31}, {"Boris", 25}, {"Alex", 31}, {"Cleo", 25}};
    std::stable_sort(people.begin(), people.end(), [](const Person& a, const Person& b) { return a.age < b.age; });
    for (const Person& p : people) std::cout << p.name << " ";
    std::cout << "\n";

    auto it = std::find_if(people.begin(), people.end(), [](const Person& p) { return p.age > 30; });
    std::cout << it->name << " " << (it - people.begin()) << "\n";

    std::vector<int> ages;
    std::transform(people.begin(), people.end(), std::back_inserter(ages), [](const Person& p) { return p.age; });
    std::cout << std::accumulate(ages.begin(), ages.end(), 0) << "\n";

    std::vector<int> sorted{1, 3, 5, 7, 9};
    std::cout << *std::lower_bound(sorted.begin(), sorted.end(), 6) << " " << std::binary_search(sorted.begin(), sorted.end(), 7)
              << "\n";
}
```
```output
Boris Cleo Dina Alex 
Dina 2
112
7 1
```

`stable_sort` сохранил порядок равных по возрасту: Boris раньше Cleo, Dina раньше Alex.

**Пример 6. Диапазоны и ленивые представления (C++20).**

```cpp run
#include <algorithm>
#include <iostream>
#include <ranges>
#include <vector>

int main() {
    std::vector<int> v{5, 2, 8, 1, 9, 4, 7};
    std::ranges::sort(v);

    auto pipeline = v | std::views::filter([](int x) { return x % 2 == 1; })
                      | std::views::transform([](int x) { return x * x; })
                      | std::views::take(3);
    for (int x : pipeline) std::cout << x << " ";
    std::cout << "\n";

    for (int x : v | std::views::reverse | std::views::take(2)) std::cout << x << " ";
    std::cout << "\n";

    auto [mn, mx] = std::ranges::minmax(v);
    std::cout << mn << " " << mx << " " << std::ranges::count_if(v, [](int x) { return x > 4; }) << "\n";
}
```
```output
1 25 49 
9 8 
1 9 4
```

Конвейер ничего не вычислял, пока по нему не прошёл цикл; контейнер `v` должен жить, пока используется `pipeline`.

**Пример 7. Ловушки (не запускать): неопределённое поведение.**

```cpp
std::vector<int> v{1, 2, 3};
int& first = v[0];
for (int i = 0; i < 100; ++i) v.push_back(i);
first = 10;                                  // ссылка висит: буфер мог переехать

for (auto it = v.begin(); it != v.end(); ++it)
    if (*it == 5) v.erase(it);               // it недействителен после erase: нужно it = v.erase(it)

std::string_view bad = std::string("temporary");   // временная строка уничтожена
std::cout << bad;                            // чтение висячего буфера

std::sort(v.begin(), v.end(), [](int a, int b) { return a <= b; });  // не строгое слабое упорядочение
```

Компилятор обычно не предупреждает ни о чём из этого. Позже, в уроке про неопределённое поведение, вы увидите, как такие ошибки находят санитайзеры.

<!-- section: mistakes -->
## Типичные ошибки

* **Держать ссылку, указатель или итератор на элемент `vector` и добавлять в него элементы.** При перевыделении они указывают в освобождённую память.
* **Удалять в цикле без `it = v.erase(it)`.** Итератор после `erase` недействителен.
* **Забыть, что `map::operator[]` вставляет.** Проверка наличия через `[]` меняет контейнер; используйте `find` или `contains`.
* **Полагаться на порядок `unordered_map`.** Он разный в разных реализациях; сортируйте перед выводом.
* **`string_view` на временную строку.** После конца выражения буфер уничтожен.
* **Передавать `string_view::data()` в функцию, ожидающую C-строку.** Нуль-терминатора может не быть.
* **Компаратор `<=` или нетранзитивный компаратор.** Нарушает строгое слабое упорядочение, `sort` может выйти за границы.
* **`std::accumulate` с начальным `0` для `double`.** Сумма считается в `int`.
* **Использовать `std::list`, потому что «вставка за O(1)».** Для небольших объёмов `vector` обычно быстрее; сначала измерьте.
* **Хранить представление (`view`) дольше контейнера.** Ленивые `views` не владеют данными.

<!-- section: summary -->
## Итоги

* Контейнеры, итераторы и алгоритмы независимы; по умолчанию берите `vector`, для ключей - `unordered_map` или `map` (если нужен порядок).
* Операция, меняющая контейнер, может инвалидировать итераторы, указатели и ссылки; правила зависят от контейнера и операции.
* `string_view` и `views` не владеют данными: следите, чтобы владелец жил дольше.
* `erase_if`, `contains`, `<ranges>` - C++20, `try_emplace` - C++17 (проверено ключами `-std`); `sort` требует строгого слабого упорядочения.

Дальше: [обработка ошибок](lesson:cpp.fundamentals.error-handling).
Назад: [шаблоны, концепты и constexpr](lesson:cpp.fundamentals.templates-concepts-constexpr).
Сравните: [коллекции и итераторы в Rust](lesson:rust.fundamentals.collections-and-iterators), [обобщения и коллекции в C#](lesson:csharp.fundamentals.generics-and-collections), [срезы и карты в Go](lesson:go.fundamentals.maps-and-slices-deep-dive), [Streams в Java](lesson:java.fundamentals.streams-lambdas-optional).
