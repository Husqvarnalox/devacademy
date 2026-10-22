<!-- section: simple -->
## Что проверяет эта проверка

Функции с параметрами `&Vec<T>`, `&mut Vec<T>` и `&str`, векторы, `HashMap` с `entry(...).or_insert(...)`, методы строк `split_once`, `trim`, `split_whitespace`, а также сортировка ключей, чтобы вывод был одинаковым при каждом запуске.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Функции: fn, параметры и результат](lesson:rust.from-zero.functions), [Векторы: список значений](lesson:rust.from-zero.vectors), [Словари HashMap: значения по ключу](lesson:rust.from-zero.hash-maps), [Методы строк: разрезаем и ищем](lesson:rust.from-zero.string-methods).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```rust run
use std::collections::HashMap;

fn parse_pair(line: &str) -> Option<(String, i32)> {
    let (name, value) = line.split_once(';')?;
    let amount: i32 = value.trim().parse().ok()?;
    Some((name.trim().to_string(), amount))
}

fn main() {
    let lines = ["food;120", "books; 300", "food;80", "bad line", "tea;x"];
    let mut totals: HashMap<String, i32> = HashMap::new();
    let mut skipped = 0;
    for line in lines {
        match parse_pair(line) {
            Some((name, amount)) => *totals.entry(name).or_insert(0) += amount,
            None => skipped += 1,
        }
    }
    let mut names: Vec<&String> = totals.keys().collect();
    names.sort();
    for name in names {
        println!("{name}: {}", totals[name]);
    }
    println!("skipped: {skipped}");
    println!("{:?} {:?}", totals.get("food"), totals.get("milk"));
}
```

```output
books: 300
food: 200
skipped: 2
Some(200) None
```

Функция `parse_pair` возвращает `Option`: оператор `?` внутри неё сразу возвращает `None`, если нет `;` или если число не разобралось. Сумма по категории копится через `*totals.entry(name).or_insert(0) += amount`: для новой категории запись создаётся со значением 0. Обход `HashMap` не упорядочен, поэтому ключи сначала собираются в вектор и сортируются. Метод `get` возвращает `Option`, а не вызывает панику.

<!-- section: precise -->
## Что нужно уметь

* объявлять функции с типами параметров и результата и понимать, что `&Vec<T>` читает, а `&mut Vec<T>` изменяет;
* вызывать функцию с `&v` и `&mut v` и не терять вектор после вызова;
* разрезать строку через `split_once`, `split`, `split_whitespace`, убирать пробелы через `trim`;
* считать по ключам через `entry(ключ).or_insert(0)` и обращаться к значению через `get` или `[ключ]`;
* печатать содержимое `HashMap` в предсказуемом порядке: сортировать ключи или брать `BTreeMap`;
* возвращать из функции `Option`, если разбор может не удаться.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Печатать `HashMap` как есть: порядок строк меняется между запусками.
* Забыть, что `map[ключ]` паникует для отсутствующего ключа; нужен `get`.
* Передавать `String` по значению в функцию и потерять переменную.
* Забыть `trim()` и получить ключ с пробелом в конце.
* Разрезать строку по каждой `;` вместо первой, когда в данных есть лишние разделители.
* Начинать поиск максимума с нуля и получить неверный ответ для отрицательных сумм.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Функции: fn, параметры и результат](lesson:rust.from-zero.functions), [Векторы: список значений](lesson:rust.from-zero.vectors), [Словари HashMap: значения по ключу](lesson:rust.from-zero.hash-maps), [Методы строк: разрезаем и ищем](lesson:rust.from-zero.string-methods). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
