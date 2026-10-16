<!-- section: simple -->
## Ошибки - это значения

В Rust нет исключений. Операция, которая может не получиться, возвращает `Result<T, E>`: либо `Ok(значение)`, либо `Err(ошибка)`. Игнорировать результат нельзя - компилятор предупредит (`Result` помечен `#[must_use]`), а доставать значение приходится явно.

Чтобы не писать `match` на каждом шаге, используют оператор `?`: если внутри `Ok`, он достаёт значение, если `Err` - немедленно возвращает ошибку из текущей функции.

```rust run
use std::num::ParseIntError;

fn parse_and_double(s: &str) -> Result<i32, ParseIntError> {
    let n: i32 = s.trim().parse()?;     // при ошибке функция сразу вернёт Err
    Ok(n * 2)
}

fn main() {
    println!("{:?}", parse_and_double(" 21 "));
    println!("{:?}", parse_and_double("abc"));

    match parse_and_double("") {
        Ok(v) => println!("значение {v}"),
        Err(e) => println!("не вышло: {e}"),
    }
}
```
```output
Ok(42)
Err(ParseIntError { kind: InvalidDigit })
не вышло: cannot parse integer from empty string
```

Есть и второй вид сбоев - **паника** (`panic!`, `unwrap()` на `None`, выход за границы массива). Паника означает «в программе баг»: по умолчанию поток разматывает стек и завершается. Простое правило: *ожидаемые* сбои (нет файла, плохой ввод пользователя, сеть) - это `Result`; *нарушенные инварианты* (то, что не должно случиться в корректной программе) - паника.

<!-- section: precise -->
## Result, ?, свои типы ошибок

**Result и Option.** `Result<T, E>` - это обычный `enum`. У него те же комбинаторы, что у `Option`: `map`, `and_then`, `unwrap_or`, `unwrap_or_else`, а ещё `map_err` (преобразовать ошибку), `ok()` (в `Option`), `ok_or` (обратно из `Option`), `is_ok_and`.

**Что делает `?`.** Выражение `expr?` для `Result` раскрывается примерно в `match expr { Ok(v) => v, Err(e) => return Err(From::from(e)) }`. Важна деталь `From::from`: ошибка **автоматически преобразуется** в тип ошибки функции, если реализован `From`. Для `Option` `?` возвращает `None`. Использовать `?` можно только в функции, возвращающей совместимый тип (`Result`, `Option`), или в `main`, возвращающей `Result<(), E>`; иначе ошибка `E0277`.

**Свой тип ошибки.** Типичный рецепт: `enum` с вариантами по источникам сбоя, `impl fmt::Display` (человекочитаемое сообщение), `impl std::error::Error` (пустой `impl`, а при вложенной причине - метод `source()`), и `impl From<ИсточникОшибки>` для каждого вложенного типа, чтобы работал `?`.

```rust
#[derive(Debug)]
enum ConfigError {
    Missing(String),
    BadNumber(std::num::ParseIntError),
}
impl std::fmt::Display for ConfigError { /* ... */ }
impl std::error::Error for ConfigError {}
impl From<std::num::ParseIntError> for ConfigError {
    fn from(e: std::num::ParseIntError) -> Self { ConfigError::BadNumber(e) }
}
```

**`Box<dyn Error>`.** Когда в функции смешиваются ошибки разных типов и вызывающему не нужно их различать (приложение, прототип, `main`), возвращают `Result<T, Box<dyn std::error::Error>>`: любая ошибка превращается в `Box<dyn Error>` через `?`, а `&str` и `String` - через `.into()`. Минус: вызывающий теряет конкретный тип (его можно восстановить через `downcast_ref::<T>()`). Для библиотек обычно выбирают собственный `enum`, чтобы вызывающий мог обработать варианты.

**Цепочка причин.** Метод `Error::source()` возвращает «внутреннюю» ошибку, а значит, можно напечатать всю цепочку сверху вниз. Реализация `Display` для обёртки обычно описывает только свой уровень, чтобы не дублировать сообщения.

**`main` возвращает Result.** `fn main() -> Result<(), E>` при `Err(e)` печатает в stderr `Error: ` и значение через `Debug` и завершает процесс с кодом 1 (при панике код 101).

**Сбор результатов.** `iter.collect::<Result<Vec<_>, _>>()` останавливается на первой ошибке; `sum`/`product` тоже работают с `Result` и `Option`.

**Паника или Result?**

* `Result` - когда вызывающий может разумно отреагировать: повторить, показать сообщение, взять значение по умолчанию. Все внешние данные (ввод, файлы, сеть) недоверенные.
* Паника - для нарушенных инвариантов, багов и примеров или тестов. Библиотечные функции вроде индексации `v[i]` паникуют, а `v.get(i)` возвращает `Option`.
* `unwrap()` допустим в тестах и там, где невозможность ошибки доказана и очевидна (например, `Regex::new` для константного шаблона, парсинг литерала); в остальных случаях пишите `expect("почему это не может случиться")` - сообщение объясняет инвариант и ускоряет разбор паники.
* Паника по умолчанию разматывает стек (`unwind`) и вызывает `Drop`; в профиле можно выбрать `panic = "abort"`. Паника в потоке завершает только его, а `join()` вернёт `Err`.

<!-- section: examples -->
## Примеры

**Пример 1. Собственный тип ошибки и `From`.**

```rust run
use std::fmt;
use std::num::ParseIntError;

#[derive(Debug)]
enum ConfigError {
    Missing(&'static str),
    BadNumber { key: &'static str, source: ParseIntError },
    OutOfRange(i64),
}

impl fmt::Display for ConfigError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ConfigError::Missing(key) => write!(f, "нет ключа {key}"),
            ConfigError::BadNumber { key, .. } => write!(f, "ключ {key} не число"),
            ConfigError::OutOfRange(n) => write!(f, "{n} вне диапазона 1..=65535"),
        }
    }
}

impl std::error::Error for ConfigError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            ConfigError::BadNumber { source, .. } => Some(source),
            _ => None,
        }
    }
}

fn port(text: &str) -> Result<u16, ConfigError> {
    let raw = text
        .lines()
        .find_map(|l| l.strip_prefix("port="))
        .ok_or(ConfigError::Missing("port"))?;
    let n: i64 = raw.parse().map_err(|e| ConfigError::BadNumber { key: "port", source: e })?;
    u16::try_from(n).map_err(|_| ConfigError::OutOfRange(n))
}

fn main() {
    for input in ["port=8080", "host=x", "port=80a", "port=70000"] {
        match port(input) {
            Ok(p) => println!("порт {p}"),
            Err(e) => {
                use std::error::Error;
                match e.source() {
                    Some(inner) => println!("ошибка: {e} (причина: {inner})"),
                    None => println!("ошибка: {e}"),
                }
            }
        }
    }
}
```
```output
порт 8080
ошибка: нет ключа port
ошибка: ключ port не число (причина: invalid digit found in string)
ошибка: 70000 вне диапазона 1..=65535
```

**Пример 2. `From` и `?` объединяют ошибки разных источников.**

```rust run
use std::fmt;
use std::num::{ParseFloatError, ParseIntError};

#[derive(Debug)]
enum ParseError {
    Int(ParseIntError),
    Float(ParseFloatError),
    Empty,
}

impl fmt::Display for ParseError {
    fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result {
        match self {
            ParseError::Int(e) => write!(f, "целое: {e}"),
            ParseError::Float(e) => write!(f, "дробное: {e}"),
            ParseError::Empty => write!(f, "пустая строка"),
        }
    }
}

impl From<ParseIntError> for ParseError {
    fn from(e: ParseIntError) -> Self { ParseError::Int(e) }
}
impl From<ParseFloatError> for ParseError {
    fn from(e: ParseFloatError) -> Self { ParseError::Float(e) }
}

fn price_times_qty(line: &str) -> Result<f64, ParseError> {
    let (price, qty) = line.split_once('x').ok_or(ParseError::Empty)?;
    let price: f64 = price.trim().parse()?;   // ParseFloatError -> ParseError
    let qty: u32 = qty.trim().parse()?;       // ParseIntError   -> ParseError
    Ok(price * f64::from(qty))
}

fn main() {
    for line in ["2.5 x 4", "2.5 x four", "abc x 1", "no separator"] {
        match price_times_qty(line) {
            Ok(v) => println!("{line:?} = {v}"),
            Err(e) => println!("{line:?}: {e}"),
        }
    }
}
```
```output
"2.5 x 4" = 10
"2.5 x four": целое: invalid digit found in string
"abc x 1": дробное: invalid float literal
"no separator": пустая строка
```

**Пример 3. `Box<dyn Error>` и `downcast_ref`.**

```rust run
use std::error::Error;

fn run(s: &str) -> Result<i32, Box<dyn Error>> {
    if s.is_empty() {
        return Err("пустой ввод".into());          // &str -> Box<dyn Error>
    }
    let n: i32 = s.parse()?;                       // ParseIntError -> Box<dyn Error>
    if n < 0 {
        return Err(format!("отрицательное число {n}").into());
    }
    Ok(n)
}

fn main() {
    for s in ["12", "", "-3", "x"] {
        match run(s) {
            Ok(n) => println!("ok {n}"),
            Err(e) => {
                let kind = if e.downcast_ref::<std::num::ParseIntError>().is_some() { "разбор" } else { "правило" };
                println!("{kind}: {e}");
            }
        }
    }
}
```
```output
ok 12
правило: пустой ввод
правило: отрицательное число -3
разбор: invalid digit found in string
```

**Пример 4. `collect` в `Result`, `Option` в `Result` и значения по умолчанию.**

```rust run
fn parse_all(items: &[&str]) -> Result<Vec<i32>, std::num::ParseIntError> {
    items.iter().map(|s| s.parse::<i32>()).collect()   // остановится на первой ошибке
}

fn main() {
    println!("{:?}", parse_all(&["1", "2", "3"]));
    println!("{:?}", parse_all(&["1", "x", "z"]));

    let total: Result<i32, _> = ["4", "5"].iter().map(|s| s.parse::<i32>()).sum();
    println!("{total:?}");

    let ok_values: Vec<i32> = ["7", "q", "9"].iter().filter_map(|s| s.parse().ok()).collect();
    println!("{ok_values:?}");

    let port: u16 = "oops".parse().unwrap_or(8080);
    let level = "9".parse::<u8>().ok().filter(|l| *l <= 5).unwrap_or_default();
    println!("{port} {level}");
}
```
```output
Ok([1, 2, 3])
Err(ParseIntError { kind: InvalidDigit })
Ok(9)
[7, 9]
8080 0
```

**Пример 5. Паника в потоке и `join`.**

```rust run
use std::thread;

fn main() {
    let h = thread::spawn(|| {
        let v: Vec<i32> = Vec::new();
        if v.is_empty() {
            return 0;       // вместо v[0]: паника не нужна
        }
        v[0]
    });
    println!("{:?}", h.join());

    // Результат join: Ok(значение) или Err(полезная нагрузка паники)
    let bad = thread::spawn(|| -> i32 { std::panic::resume_unwind(Box::new("баг")) });
    println!("{}", bad.join().is_err());
}
```
```output
Ok(0)
true
```

Как выглядят паника и `main` с ошибкой (stderr, `rustc 1.99`; в скобках идентификатор потока меняется от запуска к запуску):

```
thread 'main' (35683066) panicked at p.rs:3:32:
порт должен быть числом: ParseIntError { kind: InvalidDigit }

thread 'main' (35683319) panicked at p3.rs:4:21:
index out of bounds: the len is 3 but the index is 7

Error: ParseIntError { kind: InvalidDigit }       <- fn main() -> Result<(), ParseIntError>
```

<!-- section: mistakes -->
## Типичные ошибки

* **`unwrap()` на данных пользователя.** Внешний ввод ненадёжен; возвращайте `Result` и обрабатывайте.
* **`expect("failed")` без пояснения.** Сообщение должно объяснять, какой инвариант нарушен: `expect("порт проверен при загрузке конфига")`.
* **Использовать `String` как тип ошибки.** Вызывающий не сможет различать причины; заведите `enum`.
* **Не реализовать `From` и писать `map_err` на каждой строке.** Один `impl From` включает `?` везде.
* **Дублировать текст причины в `Display` обёртки.** Показывайте причину через `source()`, иначе сообщение повторится в цепочке дважды.
* **Использовать `?` в `main` без `Result`.** `main` должна возвращать `Result<(), E>` (ошибка `E0277`).
* **Глушить ошибку через `let _ = ...`.** Вы теряете информацию о сбое; хотя бы залогируйте.
* **Паниковать в библиотеке из-за ошибки пользователя.** Библиотека должна вернуть `Result`, решать о завершении программы вправе только приложение.

<!-- section: summary -->
## Итоги

* Ожидаемые сбои - `Result<T, E>`, баги и нарушенные инварианты - паника.
* `?` пробрасывает ошибку вверх и применяет `From`; один `impl From` избавляет от `map_err`.
* Для библиотек - собственный `enum` с `Display`, `Error` и `source()`; для приложений удобен `Box<dyn Error>`.
* `collect::<Result<Vec<_>, _>>()` останавливается на первой ошибке; `ok()`, `unwrap_or`, `unwrap_or_default` задают значения по умолчанию.
* `unwrap` - в тестах и при доказанной невозможности ошибки, иначе `expect` с объяснением.

Сравните с [ошибками-значениями в Go](lesson:go.fundamentals.functions-and-errors): там `if err != nil` пишут вручную, а в Rust `?` и тип `Result` вынуждают обработать ошибку и не позволяют использовать результат, пока вы её не проверили.
