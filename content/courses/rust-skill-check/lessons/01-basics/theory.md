<!-- section: simple -->
## Что проверяет эта проверка

Основы: переменные и типы, арифметика и целочисленное деление, чтение ввода через `read_to_string`, разбор `parse`, приведение `as`, условия `if` и `match`, циклы `while` и `for`, форматирование `{:.2}`.

## Что даёт прохождение

Проверка состоит из короткого упражнения с запуском тестов и квиза на чтение кода (порог квиза - 75%). Если пройдены обе части, засчитываются уроки: [Первая программа](lesson:rust.from-zero.first-program), [Как читать сообщения об ошибках](lesson:rust.from-zero.compiler-errors), [Вывод: print!, println! и подстановки](lesson:rust.from-zero.output), [Переменные: let и let mut](lesson:rust.from-zero.variables), [Основные типы: i64, f64, bool, char](lesson:rust.from-zero.basic-types), [Арифметика](lesson:rust.from-zero.arithmetic), [Строки: &str и String](lesson:rust.from-zero.strings), [Ввод данных: read_to_string](lesson:rust.from-zero.reading-input), [Преобразование типов: parse и as](lesson:rust.from-zero.type-conversion), [Форматированный вывод: {:.2}, ширина, выравнивание](lesson:rust.from-zero.formatted-output), [Сравнения и логика: ==, &&, ||, !](lesson:rust.from-zero.comparison-and-logic), [Условный оператор: if, else if, else](lesson:rust.from-zero.if-else), [match: выбор по образцу](lesson:rust.from-zero.match), [Цикл while и loop: повторять, пока нужно](lesson:rust.from-zero.while-loop), [Цикл for: диапазоны и строки ввода](lesson:rust.from-zero.for-loop).

Если не пройдены, ничего не отнимается: прогресс сохраняется, а система покажет первый незавершённый урок из этого списка как место для повторения. Проверку можно пройти снова в любой момент, и открыть её можно без каких-либо условий.

<!-- section: examples -->
## Пример того, что понадобится

```rust run
fn main() {
    let text = "8000 12000\n10000 x 9999";
    let mut sum: i64 = 0;
    let mut count = 0;
    let mut skipped = 0;
    for token in text.split_whitespace() {
        match token.parse::<i64>() {
            Ok(n) => {
                sum += n;
                count += 1;
            }
            Err(_) => skipped += 1,
        }
    }
    let avg = sum as f64 / count as f64;
    println!("{count} {sum} {skipped}");
    println!("{:.2} {}", avg, sum / 3);
    let big: i64 = 3_000_000_000;
    println!("{} {}", big * 2, big as i32);
}
```

```output
4 39999 1
9999.75 13333
6000000000 -1294967296
```

Строка `text` стоит на месте ввода. Цикл `for` разбирает слова по пробелам и переводам строки, `match` отделяет число от не числа, сумма хранится в `i64`: значения вида 3 000 000 000 не помещаются в `i32`. Среднее вычисляется как `f64`, поэтому перед делением обе части приводятся через `as f64`; целое деление `sum / 3` отбрасывает дробную часть. Приведение `big as i32` не проверяет значение и оставляет младшие 32 бита.

<!-- section: precise -->
## Что нужно уметь

* читать весь ввод в `String` через `std::io::read_to_string(std::io::stdin()).unwrap()` и обходить слова через `split_whitespace()`, строки - через `lines()`;
* разбирать число через `parse::<T>()` и понимать, что результат - `Result`;
* выбирать тип суммы так, чтобы она не переполнилась: `i64` вместо `i32`;
* делить как `f64` через `as f64`, а не терять дробную часть целым делением;
* печатать дробное число с двумя знаками через `{:.2}`: точка не зависит от локали;
* обновлять максимум и счётчики внутри цикла по условию (`if`), помнить про пустой ввод: деление на ноль для `f64` даёт `NaN`, а целое - панику.

<!-- section: mistakes -->
## На чём чаще всего ошибаются

* Объявить сумму как `i32` и получить заворот в сборке с `-O` или панику в отладочной.
* Делить `sum / count` для целых и потерять дробную часть либо забыть `as f64`.
* Начинать максимум с первого значения, не проверив, что значения вообще есть.
* Забыть `mut` у счётчика: компилятор остановит сборку с `E0384`.
* Среднее для пустого ввода: `0.0 / 0.0` печатает `NaN`, а не `0.00`.
* Сравнивать `>` вместо `>=` на границе в 10000 шагов.

<!-- section: summary -->
## Итог

Если задания показались трудными, начните с уроков из списка: [Первая программа](lesson:rust.from-zero.first-program), [Как читать сообщения об ошибках](lesson:rust.from-zero.compiler-errors), [Вывод: print!, println! и подстановки](lesson:rust.from-zero.output), [Переменные: let и let mut](lesson:rust.from-zero.variables), [Основные типы: i64, f64, bool, char](lesson:rust.from-zero.basic-types), [Арифметика](lesson:rust.from-zero.arithmetic), [Строки: &str и String](lesson:rust.from-zero.strings), [Ввод данных: read_to_string](lesson:rust.from-zero.reading-input), [Преобразование типов: parse и as](lesson:rust.from-zero.type-conversion), [Форматированный вывод: {:.2}, ширина, выравнивание](lesson:rust.from-zero.formatted-output), [Сравнения и логика: ==, &&, ||, !](lesson:rust.from-zero.comparison-and-logic), [Условный оператор: if, else if, else](lesson:rust.from-zero.if-else), [match: выбор по образцу](lesson:rust.from-zero.match), [Цикл while и loop: повторять, пока нужно](lesson:rust.from-zero.while-loop), [Цикл for: диапазоны и строки ввода](lesson:rust.from-zero.for-loop). После повторения вернитесь к проверке: результат не зависит от числа попыток, а засчитанное остаётся засчитанным.
