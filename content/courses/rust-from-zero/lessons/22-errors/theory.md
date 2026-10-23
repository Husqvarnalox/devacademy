<!-- section: simple -->
## Два вида ошибок

Не все ошибки одинаковы, и Rust различает их явно.

* **Ошибка программиста (баг).** Случилось то, чего по замыслу быть не должно: деление на нулевое количество, обращение за границы, «невозможное» значение. Здесь программа **останавливается** с понятным сообщением: это называется **паникой** (*panic*). Продолжать работу с нарушенными предположениями опаснее, чем остановиться.
* **Ожидаемая ошибка данных.** Пользователь ввёл `abc` вместо числа, в файле пропущено поле. Это нормальное событие, и программа должна на него **отреагировать**: сообщить, пропустить строку, попросить повторить. Здесь ошибку возвращают как значение - в типе `Result`, который вы уже разбирали.

## Одна новая идея: вернуть ошибку наружу

Функция, которая проверяет данные, возвращает `Result<i32, String>`: `Ok(число)`, если всё хорошо, и `Err(описание)`, если нет. Тот, кто вызвал функцию, решает, что делать с ошибкой. Когда одна проверка вызывает другую, ошибку нужно просто передать дальше. Для этого есть оператор `?`: «если здесь `Err`, немедленно верни его из текущей функции; если `Ok` - возьми значение и иди дальше».

<!-- section: examples -->
## Пример 1. `panic!`: остановка при нарушении предположения

```rust
fn average(total: i32, count: i32) -> i32 {
    if count == 0 {
        panic!("average: count must not be zero");
    }
    total / count
}

fn main() {
    println!("{}", average(10, 2));
    println!("{}", average(5, 0));
}
```

Ввод:

```text

```

Результат работы:

```text
thread 'main' (2060273) panicked at main.rs:3:9:
average: count must not be zero
```

Программа напечатала `5` для первого вызова и остановилась на втором: `panic!("...")` завершает программу и показывает сообщение вместе с местом (`main.rs:3:9`). Паника выводится в канал ошибок, поэтому обычный вывод (`5`) остаётся отдельно. Код завершения программы при панике не нулевой.

Паника годится для багов: функция `average` обещает, что `count` не равен нулю. Для данных, которые вводит пользователь, паника не подходит.

## Пример 2. `expect`: `unwrap` со своим сообщением

```rust
fn main() {
    let n: i32 = "abc".parse().expect("в этой строке должно быть число");
    println!("{n}");
}
```

Ввод:

```text

```

Результат работы:

```text
thread 'main' (2060341) panicked at main.rs:2:32:
в этой строке должно быть число: ParseIntError { kind: InvalidDigit }
```

`expect("текст")` делает то же, что `unwrap`: берёт значение из `Ok` / `Some`, а при `Err` / `None` останавливает программу. Разница - в сообщении: сначала ваш текст, потом причина. Так в логе понятно, какое предположение нарушено. Используйте `expect`, когда ошибка означает баг (например, строка в программе задана вами и точно число), но не для пользовательского ввода.

## Пример 3. Функция с `Result<i32, String>`

```rust run
fn parse_age(text: &str) -> Result<i32, String> {
    match text.trim().parse::<i32>() {
        Ok(n) => {
            if n < 0 || n > 150 {
                Err(format!("возраст вне диапазона: {n}"))
            } else {
                Ok(n)
            }
        }
        Err(_) => Err(format!("не число: {}", text.trim())),
    }
}

fn main() {
    for t in vec!["25", "abc", "200", " 7 "] {
        match parse_age(t) {
            Ok(age) => println!("{t:?}: возраст {age}"),
            Err(msg) => println!("{t:?}: ошибка: {msg}"),
        }
    }
}
```
```output
"25": возраст 25
"abc": ошибка: не число: abc
"200": ошибка: возраст вне диапазона: 200
" 7 ": возраст 7
```

* `-> Result<i32, String>` обещает: либо `Ok(i32)`, либо `Err(String)` с текстом причины.
* Текст ошибки собирается через `format!` (как `println!`, но возвращает `String`). В `Err` кладут именно `String`, поэтому литерал оборачивают в `String::from` или собирают через `format!`.
* Вызывающий код разбирает результат через `match`: две ветки, два вида вывода. Ни одна строка не останавливает программу.
* `{t:?}` печатает строку в кавычках, чтобы видеть пробелы: значение `" 7 "` разобралось, потому что внутри функции вызван `trim`.

## Пример 4. Оператор `?`

```rust run
fn parse_number(text: &str) -> Result<i32, String> {
    match text.trim().parse::<i32>() {
        Ok(n) => Ok(n),
        Err(_) => Err(format!("не число: {}", text.trim())),
    }
}

fn total_price(qty_text: &str, price_text: &str) -> Result<i32, String> {
    let qty = parse_number(qty_text)?;
    let price = parse_number(price_text)?;
    Ok(qty * price)
}

fn main() {
    println!("{:?}", total_price("3", "40"));
    println!("{:?}", total_price("три", "40"));
    println!("{:?}", total_price("3", "сорок"));
}
```
```output
Ok(120)
Err("не число: три")
Err("не число: сорок")
```

Строка `let qty = parse_number(qty_text)?;` значит:

* если `parse_number` вернула `Ok(n)`, переменная `qty` получает `n`;
* если вернула `Err(msg)`, **вся функция** `total_price` немедленно возвращает этот `Err(msg)`, и остальные строки не выполняются.

То же самое можно написать через `match` с `return Err(msg)`, но `?` короче. Во втором вызове ошибку вернула первая проверка, и второй `parse_number` даже не запускался. `{:?}` печатает `Ok(120)` и `Err("не число: три")`.

`?` работает только внутри функции, которая сама возвращает `Result` (с совместимым типом ошибки). В `main`, который возвращает `()`, его использовать нельзя.

## Пример 5. Проверка ввода: сообщаем об ошибках и идём дальше

Программа читает ввод, поэтому приведены ввод и вывод.

```rust
fn parse_weight(text: &str) -> Result<i32, String> {
    match text.trim().parse::<i32>() {
        Ok(n) => {
            if n > 0 {
                Ok(n)
            } else {
                Err(format!("вес должен быть больше нуля: {n}"))
            }
        }
        Err(_) => Err(format!("не число: {}", text.trim())),
    }
}

fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut line_no = 0;
    let mut total = 0;
    let mut errors = 0;
    for line in text.lines() {
        line_no += 1;
        match parse_weight(line) {
            Ok(w) => total += w,
            Err(msg) => {
                println!("строка {line_no}: {msg}");
                errors += 1;
            }
        }
    }
    println!("общий вес: {total}, ошибок: {errors}");
}
```

Ввод:

```text
12
-4
abc
 30 
0
```

Вывод:

```text
строка 2: вес должен быть больше нуля: -4
строка 3: не число: abc
строка 5: вес должен быть больше нуля: 0
общий вес: 42, ошибок: 3
```

Каждая плохая строка получает сообщение с номером, но программа не останавливается: хорошие строки учтены, итоги напечатаны. Именно так надо обращаться с данными, которым нельзя доверять.

<!-- section: precise -->
## Точные правила

* `panic!("сообщение")` останавливает программу, печатает сообщение и место в канал ошибок; код завершения ненулевой (обычно 101). Код после паники не выполняется.
* `x.unwrap()` и `x.expect("текст")` паникуют на `Err` / `None`. `expect` добавляет ваш текст к сообщению.
* Тип `Result<T, E>`: здесь `E` - `String`. Создавайте `Err(String::from("..."))` или `Err(format!("... {x}"))`; `Err("текст")` с `&str` не подходит.
* Последнее выражение функции должно быть `Ok(...)` или `Err(...)`; просто значение вернуть нельзя.
* `выражение?` внутри функции с типом `Result<T, E>`: при `Ok(v)` даёт `v`; при `Err(e)` выполняет `return Err(e)`.
* Тип ошибки у `?` и у функции должен быть совместим. Результат `parse::<i32>()` имеет тип ошибки `ParseIntError`, а не `String`, поэтому `?` на нём внутри функции с `Result<_, String>` не скомпилируется: сначала разберите его через `match` и создайте `Err(String)`, как в `parse_number`.
* `?` нельзя использовать в `main`, возвращающей `()` (стандартная запись `fn main()`).
* Результат функции с типом `Result` нельзя молча выбрасывать: компилятор предупреждает «unused `Result` that must be used».
* Для ошибок данных (ввод, файлы) возвращайте `Result`; `panic!` и `expect` оставляйте для багов.

<!-- section: mistakes -->
## Типичные ошибки

**1. `?` внутри `main`.**

```rust
fn main() {
    let n: i32 = "5".parse::<i32>()?;
    println!("{n}");
}
```

```text
error[E0277]: the `?` operator can only be used in a function that returns `Result` or `Option` (or another type that implements `FromResidual`)
 --> main.rs:2:36
  |
1 | fn main() {
  | --------- this function should return `Result` or `Option` to accept `?`
2 |     let n: i32 = "5".parse::<i32>()?;
  |                                    ^ cannot use the `?` operator in a function that returns `()`
  |
help: consider adding return type
  |
1 ~ fn main() -> Result<(), Box<dyn std::error::Error>> {
2 |     let n: i32 = "5".parse::<i32>()?;
3 |     println!("{n}");
4 +     Ok(())
  |
```

`?` возвращает `Err` из текущей функции, а `main` ничего возвращать не умеет. Разберите результат через `match` или `if let`. Компилятор предлагает изменить тип `main` на `Result<(), Box<dyn std::error::Error>>`: это допустимо, но выходит за рамки курса.

**2. Забыли `Ok(...)`.**

```rust
fn double(text: &str) -> Result<i32, String> {
    match text.parse::<i32>() {
        Ok(n) => n * 2,
        Err(_) => Err(String::from("не число")),
    }
}

fn main() {
    println!("{:?}", double("4"));
}
```

```text
error[E0308]: mismatched types
 --> main.rs:3:18
  |
1 | fn double(text: &str) -> Result<i32, String> {
  |                          ------------------- expected `Result<i32, String>` because of return type
2 |     match text.parse::<i32>() {
3 |         Ok(n) => n * 2,
  |                  ^^^^^ expected `Result<i32, String>`, found `i32`
  |
  = note: expected enum `Result<i32, String>`
             found type `i32`
help: try wrapping the expression in `Ok`
  |
3 |         Ok(n) => Ok(n * 2),
  |                  +++     +
```

Ветка `Ok(n) => n * 2` вернула число, а функция обещала `Result`. Нужно `Ok(n * 2)`.

**3. `&str` в `Err`.**

```rust
fn check(n: i32) -> Result<i32, String> {
    if n < 0 {
        return Err("отрицательное");
    }
    Ok(n)
}

fn main() {
    println!("{:?}", check(-1));
}
```

```text
error[E0308]: mismatched types
   --> main.rs:3:20
    |
  3 |         return Err("отрицательное");
    |                --- ^^^^^^^^^^^^^^^ expected `String`, found `&str`
    |                |
    |                arguments to this enum variant are incorrect
    |
help: the type constructed contains `&'static str` due to the type of the argument passed
   --> main.rs:3:16
    |
  3 |         return Err("отрицательное");
    |                ^^^^---------------^
    |                    |
    |                    this argument influences the type of `Err`
note: tuple variant defined here
   --> ...:567:5
    |
567 |     Err(#[stable(feature = "rust1", since = "1.0.0")] E),
    |     ^^^
help: try using a conversion method
    |
  3 |         return Err("отрицательное".to_string());
    |                                   ++++++++++++
```

В `Err` кладётся `String`, как и обещано в сигнатуре. Напишите `Err(String::from("отрицательное"))`.

**4. `?` на результате с другим типом ошибки.**

```rust
fn read(text: &str) -> Result<i32, String> {
    let n = text.parse::<i32>()?;
    Ok(n)
}

fn main() {
    println!("{:?}", read("5"));
}
```

```text
error[E0277]: `?` couldn't convert the error to `String`
 --> main.rs:2:32
  |
1 | fn read(text: &str) -> Result<i32, String> {
  |                        ------------------- expected `String` because of this
2 |     let n = text.parse::<i32>()?;
  |                  --------------^ the trait `From<ParseIntError>` is not implemented for `String`
  |                  |
  |                  this can't be annotated with `?` because it has type `Result<_, ParseIntError>`
  |
  = note: the question mark operation (`?`) implicitly performs a conversion on the error value using the `From` trait
  = help: `String` implements trait `From<T>`:
            From<&String>
            From<&mut str>
            From<&str>
            From<Box<str>>
            From<Cow<'_, str>>
            From<char>
```

`parse` возвращает ошибку типа `ParseIntError`, а функция - `String`; автоматически одно в другое не превратить. Разберите `parse` через `match` и создайте `Err(format!(...))`, как в примере 4.

**5. Результат функции выброшен.**

```rust
fn check(n: i32) -> Result<i32, String> {
    if n < 0 {
        return Err(String::from("отрицательное"));
    }
    Ok(n)
}

fn main() {
    check(-5);
    println!("готово");
}
```

```text
warning: unused `Result` that must be used
 --> main.rs:9:5
  |
9 |     check(-5);
  |     ^^^^^^^^^
  |
  = note: this `Result` may be an `Err` variant, which should be handled
  = note: `#[warn(unused_must_use)]` (part of `#[warn(unused)]`) on by default
help: use `let _ = ...` to ignore the resulting value
  |
9 |     let _ = check(-5);
  |     +++++++
```

Программа скомпилируется и напечатает `готово`, но проверка ничего не изменила: ошибка пропала. Разберите результат через `match` / `if let` или явно покажите, что игнорируете: `let _ = check(-5);`.

<!-- section: summary -->
## Итоги

* Баг - паника: `panic!("...")`, `unwrap()`, `expect("...")`. Ошибка данных - значение `Result<T, String>`.
* `Err(String::from(...))` или `Err(format!(...))` описывает проблему; `Ok(значение)` - успех.
* Оператор `?` внутри функции с `Result`: на `Ok` даёт значение, на `Err` немедленно возвращает ошибку из функции.
* Ошибки ввода обрабатывайте и считайте, а не паникуйте; `expect` оставьте для невозможных ситуаций.
* Результат функции с `Result` нужно использовать: компилятор предупреждает, если его выбросили.

Следующий урок: [первая структура](lesson:rust.from-zero.first-struct): как объединить связанные данные в один тип, чтобы не передавать их по отдельности.

<!-- section: programmer -->
## Для тех, кто уже программирует

* В Rust нет исключений. Ожидаемые ошибки - значения `Result<T, E>` в сигнатуре, паника - для неустранимых багов. Паника по умолчанию разматывает стек (можно настроить `panic = "abort"`) и может быть перехвачена только на границе потока.
* Оператор `?` - не магия. Он разворачивается в `match` и `return Err(From::from(e))`: ошибка при возврате **преобразуется** в тип ошибки функции, если для этого есть реализация `From`. Поэтому в реальных программах `?` часто работает на разных типах ошибок (`Box<dyn Error>`, `anyhow::Error`, свой `enum` с `thiserror`). Это тема `rust-fundamentals` (`error-handling`).
* Здесь используется `Result<T, String>` как самый простой вариант: понятен и даёт читаемое сообщение. Для библиотек обычно создают собственный тип ошибки.
* `fn main() -> Result<(), E>` допустима: возвращённая `Err` печатается и даёт ненулевой код выхода. В этом курсе `main` остаётся `fn main()`.
* `expect("...")` принято писать так, чтобы сообщение объясняло, **почему ошибка невозможна**, а не что произошло.
* Комбинаторы `map_err`, `ok_or`, `and_then`, `unwrap_or_else` с замыканиями отложены до `rust-fundamentals`.

<!-- section: deep-dive -->
## Что делает `?` на самом деле

Это необязательный раздел: для заданий он не нужен.

Строка `let qty = parse_number(qty_text)?;` эквивалентна такой записи:

```rust
let qty = match parse_number(qty_text) {
    Ok(v) => v,
    Err(e) => return Err(e),
};
```

В общем случае вместо `Err(e)` возвращается `Err(From::from(e))`: если типы ошибок совпадают, преобразование ничего не делает, а если нет, вызывается подходящая реализация. Именно поэтому `?` на `ParseIntError` в функции с `String` не скомпилируется: для такой пары реализации нет.

**Паника и размотка стека.** По умолчанию при панике Rust проходит вверх по стеку вызовов и освобождает локальные переменные (вызывает их деструкторы), затем завершает поток; в главном потоке это завершает процесс с кодом 101. Размотка нужна для освобождения ресурсов (закрытия файлов, снятия блокировок). Если в `Cargo.toml` указать `panic = "abort"`, процесс завершится сразу, без размотки: двоичный файл меньше и быстрее, но ресурсы освобождает операционная система.
