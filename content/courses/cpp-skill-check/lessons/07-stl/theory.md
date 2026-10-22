<!-- section: simple -->
## Что проверяет эта проверка

Контейнеры `std::vector`, `std::map`, `std::unordered_map`, `std::string` и `std::string_view`, итераторы и их инвалидацию, стандартные алгоритмы (`std::sort` с лямбдой, `std::remove_if` вместе с `erase`, `std::erase_if`, `std::count_if`, `std::accumulate`), операции `operator[]` и `count` у `map`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитывается урок [Контейнеры STL, итераторы и алгоритмы](lesson:cpp.fundamentals.stl-containers-algorithms).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет этот урок как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```cpp run
#include <algorithm>
#include <iostream>
#include <map>
#include <numeric>
#include <string>
#include <string_view>
#include <vector>

int main() {
    std::vector<int> v{5, -1, 3, -2, 8, 3};
    auto newEnd = std::remove_if(v.begin(), v.end(), [](int x) { return x < 0; });
    std::cout << v.size() << " ";
    v.erase(newEnd, v.end());
    std::cout << v.size() << "\n";

    std::sort(v.begin(), v.end(), [](int a, int b) { return a > b; });
    for (std::size_t i = 0; i < v.size(); ++i) {
        std::cout << (i == 0 ? "" : " ") << v[i];
    }
    std::cout << "\n";

    std::map<std::string, int> counts;
    for (std::string w : {"b", "a", "b"}) {
        ++counts[w];
    }
    std::cout << counts["a"] << " " << counts["b"] << " " << counts.count("z") << " " << counts.size() << "\n";

    long long sum = std::accumulate(v.begin(), v.end(), 0LL);
    auto big = std::count_if(v.begin(), v.end(), [](int x) { return x > 3; });
    std::cout << sum << " " << big << "\n";

    std::string text = "hello world";
    std::string_view word = std::string_view(text).substr(0, 5);
    std::cout << word << "\n";
}
```
```output
6 4
8 5 3 3
1 2 0 2
19 2
hello
```

`remove_if` только переставляет элементы и возвращает новый логический конец, размер меняет уже `erase`. Обращение `counts["z"]` создало бы запись, а `count("z")` её не создаёт. Начальное значение `0LL` задаёт тип суммы: с `0` сумма считалась бы в `int`. `std::string_view` не владеет текстом, поэтому строка `text` должна жить дольше, чем `word`.

<!-- section: precise -->
## Что нужно уметь

* удалять элементы идиомой `v.erase(std::remove_if(...), v.end())` или `std::erase_if(v, ...)`, знать, что один `remove_if` размер не меняет;
* сортировать `std::sort` с лямбдой-компаратором (строгий порядок: `<` или `>`, но не `<=`);
* знать, что `map::operator[]` добавляет отсутствующий ключ, а `count` и `find` - нет;
* понимать, что `insert`, `push_back` и `erase` могут сделать итераторы и ссылки недействительными, а `it = v.erase(it)` возвращает следующий итератор;
* выбирать тип начального значения в `std::accumulate`: от него зависит тип суммы;
* не хранить `std::string_view` на временную строку или на строку, которая уже уничтожена.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* `std::remove` без `erase`: размер контейнера не меняется, в хвосте остаются «лишние» элементы.
* Использование итератора после `erase` или `push_back`: неопределённое поведение.
* Изменение `std::vector` внутри цикла `for (auto x : v)`, который по нему проходит.
* `std::accumulate(..., 0)` для больших значений: суммирование идёт в `int` и переполняется.
* Обращение `m[key]` ради проверки наличия: ключ добавляется со значением по умолчанию.
* `std::string_view`, созданный из временной `std::string`: после конца выражения строка уничтожена.
* Компаратор `<=` в `std::sort`: нарушает требование строгого порядка.

<!-- section: summary -->
## Итог

Если задания показались трудными, повторите урок [Контейнеры STL, итераторы и алгоритмы](lesson:cpp.fundamentals.stl-containers-algorithms). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
