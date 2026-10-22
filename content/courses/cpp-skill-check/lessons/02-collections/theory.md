<!-- section: simple -->
## Что проверяет эта проверка

Функции с параметрами: передача по значению, по `T&` и по `const T&`; `std::vector` и обход по индексу и через range-for; `std::map` со счётчиками и суммами; строки: `find`, `substr`, `getline` и разбор строки вида `имя;число`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Функции](lesson:cpp.from-zero.functions), [Передача по значению и по ссылке](lesson:cpp.from-zero.references), [Контейнер vector](lesson:cpp.from-zero.vector), [Контейнер map](lesson:cpp.from-zero.map), [Строки ввода: getline, find и substr](lesson:cpp.from-zero.text-lines).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```cpp run
#include <iostream>
#include <map>
#include <string>
#include <vector>

void addOne(std::vector<int>& values) {
    for (int& value : values) {
        value += 1;
    }
}

int total(const std::vector<int>& values) {
    int sum = 0;
    for (int value : values) {
        sum += value;
    }
    return sum;
}

int main() {
    std::vector<int> numbers = {3, 5, 8};
    addOne(numbers);
    std::cout << total(numbers) << " " << numbers.size() << "\n";

    std::map<std::string, int> counts;
    for (const std::string word : {"b", "a", "b", "c", "b"}) {
        counts[word]++;
    }
    for (const auto& [word, count] : counts) {
        std::cout << word << "=" << count << "\n";
    }
    std::cout << counts.count("z") << " " << counts.size() << "\n";

    std::string line = "Anna;90";
    std::size_t semicolon = line.find(';');
    std::string name = line.substr(0, semicolon);
    int score = std::stoi(line.substr(semicolon + 1));
    std::cout << name << " " << score + 1 << "\n";
}
```
```output
19 3
a=1
b=3
c=1
0 3
Anna 91
```

Функция `addOne` принимает вектор по ссылке и меняет оригинал, `total` только читает и принимает `const&`. Запись `counts[word]++` создаёт счётчик со значением `0` при первом обращении и увеличивает его; `count("z")` ничего не вставляет. Словарь обходится по алфавиту, а `const auto& [word, count]` распаковывает пару «ключ - значение». Метод `find` возвращает позицию символа, `substr` вырезает часть строки.

<!-- section: precise -->
## Что нужно уметь

* выбирать между `T`, `T&` и `const T&` для параметров и объяснять, какая версия меняет оригинал;
* обходить `std::vector` через `for (std::size_t i = 0; ...)` и range-for, различать `for (int x : v)` и `for (int& x : v)`;
* использовать `push_back`, `size`, `at` и знать, что `v[i]` не проверяет границы;
* накапливать значения в `std::map<std::string, int>` через `m[key] += ...` и знать, что `m[key]` вставляет запись;
* обходить словарь и искать наибольший элемент;
* искать в строке через `find` с проверкой `std::string::npos` и вырезать части через `substr`;
* читать строки целиком через `std::getline` и помнить ловушку «`>>`, затем `getline`».

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Изменение копии: параметр без `&` или переменная цикла без `&`.
* Параметр `T&` там, где достаточно `const T&`: такой функции нельзя передать временное значение.
* `v[i]` или `v.at(i)` с индексом, равным `v.size()`.
* Вычитание из `size()`: `v.size() - 1` для пустого вектора - огромное число.
* Проверка `if (m["x"] == 0)`, которая вставляет ключ.
* Сравнение результата `find` с `-1` вместо `std::string::npos`.
* `getline` сразу после `>>` читает пустой остаток строки.
* Обращение к `substr(pos)` с позицией дальше конца строки: исключение `std::out_of_range`.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Функции](lesson:cpp.from-zero.functions), [Передача по значению и по ссылке](lesson:cpp.from-zero.references), [Контейнер vector](lesson:cpp.from-zero.vector), [Контейнер map](lesson:cpp.from-zero.map), [Строки ввода: getline, find и substr](lesson:cpp.from-zero.text-lines). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
