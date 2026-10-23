<!-- section: simple -->
## Зачем нужен тип «значения может не быть»

Многие операции не всегда способны дать ответ. Нельзя разделить на ноль. Нельзя прочитать следующую строку, когда ввод кончился. Нельзя превратить слово «абв» в число. Языки решают это по-разному: возвращают `null`, бросают исключение, возвращают специальное число вроде `-1`. Все эти способы объединяет одно: ничто не заставляет вызывающий код проверить, что с результатом всё в порядке, и забытая проверка оборачивается падением программы.

Rust делает возможность отсутствия частью **типа**. Результат, которого может не быть, имеет особый тип, и компилятор не даст использовать его как обычное число, пока вы не разберёте оба случая. Вы уже встречались с этим: `lines.next()` и `parse()` возвращают такие значения, а `unwrap()` «разворачивал» их, останавливая программу при неудаче.

## Одна новая идея: два типа-обёртки

* **`Option<T>`** - «значение типа `T` или ничего»: либо `Some(значение)`, либо `None`. Так возвращают результат, когда причина отсутствия очевидна: нет следующей строки, нет такого элемента.
* **`Result<T, E>`** - «значение типа `T` или ошибка типа `E`»: либо `Ok(значение)`, либо `Err(причина)`. Так возвращают результат, когда причину важно знать: `parse()` объясняет, что именно не так с текстом.

`T` и `E` - типы внутри: `Option<i32>` читается «`Option` из `i32`». Разобрать такое значение удобно оператором `match` из урока про выбор: у каждого из двух вариантов своя ветка.

<!-- section: examples -->
## Пример 1. `Result` от `parse()`

```rust run
fn check(text: &str) {
    match text.parse::<i32>() {
        Ok(n) => println!("{text:?} -> число {}", n * 2),
        Err(e) => println!("{text:?} -> не число: {e}"),
    }
}

fn main() {
    check("21");
    check("abc");
    check("");
    check("3.5");
}
```
```output
"21" -> число 42
"abc" -> не число: invalid digit found in string
"" -> не число: cannot parse integer from empty string
"3.5" -> не число: invalid digit found in string
```

* `text.parse::<i32>()` - «разобрать текст как `i32`». Запись `::<i32>` указывает тип прямо в вызове; раньше тот же тип указывался слева: `let n: i32 = ...`.
* Результат имеет тип `Result<i32, ...>`: `Ok(n)` с числом или `Err(e)` с описанием ошибки. Ветки `match` обрабатывают оба случая, и ни одна не падает.
* `{text:?}` печатает текст в кавычках, чтобы было видно пустую строку (`{:?}` - «отладочный» вывод, подробнее в уроке про векторы).
* Вывод `e` через `{e}` даёт готовое английское описание: `invalid digit found in string`, `cannot parse integer from empty string`. Заметьте, что `3.5` для `i32` - тоже ошибка.

**Попробуйте.** Добавьте вызовы `check("-7")`, `check(" 5")` и `check("+9")`. Какие из них разберутся, а какие нет?

## Пример 2. Своя функция возвращает `Option`

```rust run
fn safe_divide(a: i32, b: i32) -> Option<i32> {
    if b == 0 {
        None
    } else {
        Some(a / b)
    }
}

fn main() {
    match safe_divide(10, 2) {
        Some(result) => println!("10 / 2 = {result}"),
        None => println!("делить на ноль нельзя"),
    }
    match safe_divide(10, 0) {
        Some(result) => println!("10 / 0 = {result}"),
        None => println!("делить на ноль нельзя"),
    }

    if let Some(result) = safe_divide(9, 3) {
        println!("9 / 3 = {result}");
    }
    println!("{}", safe_divide(1, 0).is_none());
}
```
```output
10 / 2 = 5
делить на ноль нельзя
9 / 3 = 3
true
```

* `fn safe_divide(...) -> Option<i32>` честно говорит в сигнатуре: ответ может отсутствовать. Внутри функция возвращает `Some(a / b)` или `None`.
* `match` с ветками `Some(result)` и `None` разбирает оба случая. Имя `result` получает значение из `Some`.
* `if let Some(result) = ... { ... }` - короткая запись, когда интересен только случай `Some`: «если результат - `Some`, возьми значение и выполни блок». Остальные случаи молча пропускаются (или идут в `else`).
* `.is_none()` и `.is_some()` отвечают `bool` на вопрос «пусто?» и «есть значение?».

## Пример 3. `unwrap_or`: значение по умолчанию

```rust run
fn main() {
    let a = "17".parse::<i32>().unwrap_or(0);
    let b = "семнадцать".parse::<i32>().unwrap_or(0);
    println!("{a} {b}");

    let missing: Option<i32> = None;
    println!("{}", missing.unwrap_or(-1));
    println!("{}", Some(5).unwrap_or(-1));
}
```
```output
17 0
-1
5
```

`x.unwrap_or(запасное)` возвращает значение из `Some` / `Ok`, а если его нет - запасное. Это самый короткий способ сказать «если не вышло, пусть будет 0». Подходит, когда подходящее запасное значение действительно существует; если неудачу нужно заметить (посчитать, сообщить), используйте `match`.

## Пример 4. Ввод с ошибками: считаем плохие строки

Программа читает ввод, поэтому приведены ввод и вывод.

```rust
fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut total = 0;
    let mut bad = 0;
    for line in text.lines() {
        match line.trim().parse::<i32>() {
            Ok(n) => total += n,
            Err(_) => bad += 1,
        }
    }
    println!("Сумма: {total}, неверных строк: {bad}");
}
```

Ввод:

```text
10
абв
 5 
2.5
-3
```

Вывод:

```text
Сумма: 12, неверных строк: 2
```

* Цикл `for` перебирает строки, `match` разбирает результат `parse`.
* В ветке `Ok(n)` значение из результата получает имя `n`; в ветке `Err(_)` причина ошибки не нужна, и `_` значит «любое, имя не требуется».
* Строка `абв` и `2.5` - неверные. Строка ` 5 ` разобралась, потому что перед `parse` вызван `trim()`.
* Программа не падает, а считает плохие строки. Именно так надо работать с вводом пользователя.

## Пример 5. Конец ввода и `Option`

```rust
fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = text.lines();
    let first = lines.next().unwrap_or("");
    println!("Первая строка: [{first}]");
    let second = lines.next();
    if let Some(line) = second {
        println!("Вторая строка: [{line}]");
    } else {
        println!("Второй строки нет");
    }
}
```

Ввод:

```text
привет
```

Вывод:

```text
Первая строка: [привет]
Второй строки нет
```

`lines.next()` возвращает `Option`: после последней строки наступает `None`. `unwrap_or("")` даёт пустую строку, если первой нет, а `if let Some(line) = second` обрабатывает вторую строку только тогда, когда она есть.

<!-- section: precise -->
## Точные правила

* `Option<T>` - либо `Some(значение)`, либо `None`. `Result<T, E>` - либо `Ok(значение)`, либо `Err(ошибка)`.
* Это обычные значения. Из них нельзя просто «достать» число: компилятор требует разобрать оба варианта.
* `match` над `Option` / `Result` должен покрывать оба варианта (`Some` и `None`, `Ok` и `Err`).
* `if let Образец = значение { ... } else { ... }` - сокращение `match` для одного образца; ветка `else` необязательна.
* `x.unwrap_or(запас)` - значение или запасной вариант того же типа. `x.is_some()`, `x.is_none()`, `x.is_ok()`, `x.is_err()` дают `bool`.
* `x.unwrap()` берёт значение, а при `None` или `Err` останавливает программу сообщением (*паника*). Допустим, когда ошибка невозможна по логике программы, но не для пользовательского ввода.
* Тип в `parse::<Тип>()` задаёт результат; без типа (ни слева, ни в записи `::<Тип>`) компилятор не поймёт, во что разбирать.
* Если функция может не вернуть ответ, объявите `-> Option<T>`, а возвращайте `Some(...)` / `None`. Просто значение вернуть нельзя: тип другой.
* Ошибка `parse` для целых: пустая строка, пробелы, буквы, знаки в середине, дробная часть, число за пределами диапазона `i32`.

<!-- section: mistakes -->
## Типичные ошибки

**1. Арифметика над `Option`.**

```rust
fn safe_divide(a: i32, b: i32) -> Option<i32> {
    if b == 0 { None } else { Some(a / b) }
}

fn main() {
    let x = safe_divide(10, 2);
    println!("{}", x + 1);
}
```

```text
error[E0369]: cannot add `{integer}` to `Option<i32>`
   --> main.rs:7:22
    |
  7 |     println!("{}", x + 1);
    |                    - ^ - {integer}
    |                    |
    |                    Option<i32>
    |
note: `Option<i32>` does not implement `Add<{integer}>`
   --> ...:598:1
    |
598 | pub enum Option<T> {
    | ^^^^^^^^^^^^^^^^^^ `Option<i32>` is defined in another crate
```

`x` - не число, а «число или ничего». Сначала его нужно разобрать: `match`, `if let`, `unwrap_or`.

**2. Печать `Result` через `{}`.**

```rust
fn main() {
    let x = "5".parse::<i32>();
    println!("{}", x);
}
```

```text
error[E0277]: `Result<i32, ParseIntError>` doesn't implement `std::fmt::Display`
 --> main.rs:3:20
  |
3 |     println!("{}", x);
  |               --   ^ `Result<i32, ParseIntError>` cannot be formatted with the default formatter
  |               |
  |               required by this formatting parameter
  |
  = help: the trait `std::fmt::Display` is not implemented for `Result<i32, ParseIntError>`
  = note: in format strings you may be able to use `{:?}` (or `{:#?}` for pretty-print) instead
```

`Result` нельзя печатать как есть. Разберите его или используйте отладочный вывод `{:?}` (следующий урок).

**3. `match` не покрывает `None`.**

```rust
fn main() {
    let x: Option<i32> = Some(3);
    match x {
        Some(n) => println!("{n}"),
    }
}
```

```text
error[E0004]: non-exhaustive patterns: `None` not covered
   --> main.rs:3:11
    |
  3 |     match x {
    |           ^ pattern `None` not covered
    |
note: `Option<i32>` defined here
   --> ...:598:1
    |
598 | pub enum Option<T> {
    | ^^^^^^^^^^^^^^^^^^
...
602 |     None,
    |     ---- not covered
    = note: the matched value is of type `Option<i32>`
help: ensure that all possible cases are being handled by adding a match arm with a wildcard pattern or an explicit pattern as shown
    |
  4 ~         Some(n) => println!("{n}"),
  5 ~         None => todo!(),
    |
```

Компилятор называет недостающий случай. Добавьте ветку `None => ...` или `_ => ...`.

**4. Вернули значение вместо `Some(значение)`.**

```rust
fn find_even(a: i32) -> Option<i32> {
    if a % 2 == 0 {
        return a;
    }
    None
}

fn main() {
    println!("{:?}", find_even(4));
}
```

```text
error[E0308]: mismatched types
 --> main.rs:3:16
  |
1 | fn find_even(a: i32) -> Option<i32> {
  |                         ----------- expected `Option<i32>` because of return type
2 |     if a % 2 == 0 {
3 |         return a;
  |                ^ expected `Option<i32>`, found `i32`
  |
  = note: expected enum `Option<i32>`
             found type `i32`
help: try wrapping the expression in `Some`
  |
3 |         return Some(a);
  |                +++++ +
```

Функция обещала `Option<i32>`, а вернула `i32`. Компилятор подсказывает обернуть значение в `Some(...)`.

**5. Запасное значение другого типа.**

```rust
fn main() {
    let n = "5".parse::<i32>().unwrap_or("0");
    println!("{n}");
}
```

```text
error[E0308]: mismatched types
    --> main.rs:2:42
     |
   2 |     let n = "5".parse::<i32>().unwrap_or("0");
     |                                --------- ^^^ expected `i32`, found `&str`
     |                                |
     |                                arguments to this method are incorrect
     |
help: the return type of this call is `&'static str` due to the type of the argument passed
    --> main.rs:2:13
     |
   2 |     let n = "5".parse::<i32>().unwrap_or("0");
     |             ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^---^
     |                                          |
     |                                          this argument influences the return type of `unwrap_or`
note: method defined here
    --> ...:1589:18
     |
1589 |     pub const fn unwrap_or(self, default: T) -> T
     |                  ^^^^^^^^^
```

`unwrap_or` должен получить значение того же типа, что и внутри результата: `i32`, а не текст.

**6. `unwrap` на плохом вводе.** Программа компилируется, но падает при запуске:

```rust
fn main() {
    let n: i32 = "abc".parse().unwrap();
    println!("{n}");
}
```

Ввод:

```text

```

Результат работы:

```text
thread 'main' (2029146) panicked at main.rs:2:32:
called `Result::unwrap()` on an `Err` value: ParseIntError { kind: InvalidDigit }
```

Сообщение называет место (`main.rs:2:32`), метод и причину. Для пользовательского ввода вместо `unwrap` разбирайте результат через `match`.

<!-- section: summary -->
## Итоги

* `Option<T>` - `Some(значение)` или `None`; `Result<T, E>` - `Ok(значение)` или `Err(ошибка)`. Оба типа заставляют обработать отсутствие результата.
* Разбирают их через `match` (оба случая), `if let` (один случай) и `unwrap_or` (запасное значение).
* `parse::<i32>()` возвращает `Result`; `lines.next()` и поиск возвращают `Option`; свои функции с «может не быть» возвращают `Option`.
* `unwrap()` падает на неудаче; для пользовательского ввода его заменяют обработкой.
* Нельзя вычислять с `Option` и печатать его через `{}`: сначала разберите.

Следующий урок: [векторы](lesson:rust.from-zero.vectors): хранение нескольких значений в одной переменной.

<!-- section: programmer -->
## Для тех, кто уже программирует

* В Rust нет `null` и нет исключений для обычных ошибок. Отсутствие - `Option<T>`, ошибка - `Result<T, E>`; обе - обычные перечисления (`enum`) в стандартной библиотеке.
* Компилятор требует обработки: значение нельзя использовать, не разобрав варианты. Это замена проверкам на `null` и `try/catch`; забыть обработку нельзя, не заметив этого.
* `unwrap()` - аналог необработанного исключения (паника); `expect("сообщение")` - то же с вашим текстом (в уроке про ошибки). `unwrap_or`, `unwrap_or_else`, `map`, `and_then`, оператор `?` - способы работать с значением без развёртывания (часть - в `rust-fundamentals`).
* `Option` не имеет накладных расходов для ссылок и `Box`: `None` кодируется нулевым указателем («niche optimization»).
* `if let` и `let ... else` - короткие формы `match`, когда важен один случай.
* `parse::<T>()` возвращает `Result<T, T::Err>`: тип ошибки зависит от целевого типа (`ParseIntError`, `ParseFloatError`).
* Паника в релизной сборке (`-O`) по умолчанию размотает стек и завершит поток; в приложении это завершает процесс с ненулевым кодом.

<!-- section: deep-dive -->
## Откуда берутся `Some` и `Ok`

Это необязательный раздел: для заданий он не нужен.

`Option` и `Result` - не встроенная магия, а обычные **перечисления** (`enum`): типы, значение которых - это один из нескольких заранее перечисленных вариантов, причём у варианта могут быть данные. В стандартной библиотеке они объявлены примерно так:

```rust
enum Option<T> {
    None,
    Some(T),
}

enum Result<T, E> {
    Ok(T),
    Err(E),
}
```

Поэтому `match` по ним ничем не отличается от `match` по вашему собственному перечислению (его вы встретите в `rust-fundamentals`), а компилятор следит за полнотой так же, как для чисел.

`T` и `E` - **обобщённые параметры**: `Option` один и тот же для `i32`, `String` и чего угодно, а тип внутри подставляется при использовании. Отсюда записи `Option<i32>` и `Result<i32, ParseIntError>`. Подробнее об обобщениях - в `rust-fundamentals`.
