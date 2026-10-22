<!-- section: simple -->
## Зачем этот урок

В Rust нет `null` и нет исключений. Значение, которого может не быть, имеет тип `Option<T>`; операция, которая может завершиться ошибкой, возвращает `Result<T, E>`. Оба типа - обычные перечисления, и компилятор не даст использовать значение, не обработав вариант «нет» или «ошибка». Для данных используют `struct` с полями и блок `impl` с методами. Успешное прохождение этого урока (упражнение и квиз) засчитывает уроки 18, 22 и 23 курса «Rust с нуля».

## Главная мысль

Ошибка - это значение, которое видно в сигнатуре функции. Функция `parse_score(text: &str) -> Result<i32, String>` честно сообщает: «я могу не получить число». Вызывающий код либо разбирает результат через `match`, либо передаёт ошибку выше оператором `?`. Паника (`unwrap`, индекс за границей) - это не обработка ошибки, а аварийная остановка для ситуаций, которые считаются багом программы.

<!-- section: examples -->
## Пример 1. `Option`

```rust run
fn first_even(values: &Vec<i32>) -> Option<i32> {
    for v in values {
        if v % 2 == 0 {
            return Some(*v);
        }
    }
    None
}

fn main() {
    let v = vec![1, 3, 4, 6];
    match first_even(&v) {
        Some(n) => println!("чётное: {n}"),
        None => println!("чётных нет"),
    }
    println!("{:?} {:?}", first_even(&v), first_even(&vec![1, 3]));

    if let Some(last) = v.last() {
        println!("последний: {last}");
    }
    println!("{}", v.get(9).copied().unwrap_or(-1));

    let name: Option<&str> = Some("Анна");
    let nobody: Option<&str> = None;
    println!("{} {}", name.map(|n| n.len()).unwrap_or(0), nobody.map(|n| n.len()).unwrap_or(0));
    println!("{} {}", name.is_some(), nobody.is_none());
    println!("{:?}", "42".parse::<i32>());
    println!("{:?}", "4x".parse::<i32>());
}
```

```output
чётное: 4
Some(4) None
последний: 6
-1
8 0
true true
Ok(42)
Err(ParseIntError { kind: InvalidDigit })
```

Разбор. `Some(значение)` и `None` - два варианта `Option<T>`. Оператор `match` обязан обработать оба. `if let Some(x) = ...` - короткая запись, когда нужен только один вариант. `get(9)` вернул `None`, потому что девятого элемента нет, а `copied()` превращает `Option<&i32>` в `Option<i32>`, после чего `unwrap_or(-1)` подставляет значение по умолчанию. Метод `map` применяет замыкание внутри `Some` и оставляет `None` без изменений. Результат разбора числа - `Result`: `Ok(42)` в случае успеха и `Err(...)` с описанием причины, если текст не число.

**Попробуйте.** Замените `v.get(9)` на `v[9]` и сравните поведение: индексация остановит программу паникой, а `get` позволяет решить на месте.

## Пример 2. `Result`, `?` и паника

```rust run
fn parse_percent(text: &str) -> Result<i32, String> {
    let n: i32 = text.trim().parse().map_err(|_| format!("не число: {}", text.trim()))?;
    if n < 0 || n > 100 {
        return Err(format!("вне диапазона: {n}"));
    }
    Ok(n)
}

fn average(a: &str, b: &str) -> Result<i32, String> {
    let x = parse_percent(a)?;
    let y = parse_percent(b)?;
    Ok((x + y) / 2)
}

fn main() {
    println!("{:?}", parse_percent(" 80 "));
    println!("{:?}", parse_percent("abc"));
    println!("{:?}", parse_percent("120"));
    println!("{:?}", average("70", "91"));
    println!("{:?}", average("70", "-5"));

    match "12x".parse::<i32>() {
        Ok(n) => println!("число {n}"),
        Err(e) => println!("ошибка: {e}"),
    }
    match average("90", "x") {
        Ok(n) => println!("среднее {n}"),
        Err(msg) => println!("не удалось: {msg}"),
    }
}
```

```output
Ok(80)
Err("не число: abc")
Err("вне диапазона: 120")
Ok(80)
Err("вне диапазона: -5")
ошибка: invalid digit found in string
не удалось: не число: x
```

Разбор. Результат `Result<i32, String>` означает «целое число или текст ошибки». Метод `parse` возвращает собственную ошибку разбора; `map_err` заменяет её на понятный `String`, чтобы типы совпали. Оператор `?` после выражения означает: «если здесь `Ok(x)`, возьми `x`; если `Err(e)`, немедленно верни этот `Err` из текущей функции». Благодаря ему функция `average` не содержит ни одного `match`. `?` работает только внутри функции, которая сама возвращает `Result` (или `Option`). Записи `{:?}` печатают `Ok(...)` и `Err(...)` в отладочном виде, а `{e}` для ошибки разбора выводит её человеческое описание.

Панику показывает отдельная программа: остановка происходит при разборе строки без числа.

```rust
fn main() {
    let text = "n/a";
    println!("начало");
    let n: i32 = text.parse().unwrap();
    println!("{n}");
}
```

```text
thread 'main' panicked at main.rs:4:31:
called `Result::unwrap()` on an `Err` value: ParseIntError { kind: InvalidDigit }
```

Метод `unwrap` забирает значение из `Ok` или `Some`, а на `Err` и `None` останавливает программу. Это уместно в тестах и в местах, где ошибка невозможна по логике; для пользовательского ввода берите `match`, `unwrap_or` или `?`. Метод `expect("сообщение")` делает то же, но добавляет ваше объяснение.

## Пример 3. Структуры

```rust run
#[derive(Debug, Clone)]
struct Student {
    name: String,
    score: i32,
}

impl Student {
    fn new(name: &str, score: i32) -> Student {
        Student { name: String::from(name), score }
    }

    fn passed(&self) -> bool {
        self.score >= 60
    }

    fn add_bonus(&mut self, bonus: i32) {
        self.score += bonus;
    }
}

fn best(students: &Vec<Student>) -> Option<&Student> {
    let mut result: Option<&Student> = None;
    for s in students {
        match result {
            Some(current) if current.score >= s.score => {}
            _ => result = Some(s),
        }
    }
    result
}

fn main() {
    let mut anna = Student::new("Анна", 58);
    println!("{} {}", anna.name, anna.passed());
    anna.add_bonus(5);
    println!("{} {}", anna.score, anna.passed());
    println!("{:?}", anna);

    let copy = anna.clone();
    anna.add_bonus(20);
    println!("{} {}", copy.score, anna.score);

    let mut group = vec![anna, Student::new("Борис", 90), Student::new("Вера", 90)];
    println!("{:?}", best(&group).map(|s| s.name.as_str()));
    group.sort_by(|a, b| b.score.cmp(&a.score));
    for s in &group {
        println!("{:<6}{:>4}", s.name, s.score);
    }
    println!("{}", best(&Vec::new()).is_none());
}
```

```output
Анна false
63 true
Student { name: "Анна", score: 63 }
63 83
Some("Борис")
Борис   90
Вера    90
Анна    83
true
```

Разбор. Структура объединяет поля разных типов; создаётся записью `Student { name, score }` (если имя поля совпадает с именем переменной, его не повторяют). Методы пишут в блоке `impl`. Первый параметр `&self` означает «читаю», `&mut self` - «изменяю» (вызывающая переменная должна быть `mut`), а функция без `self`, как `new`, - **ассоциированная**: вызывается через `Student::new(...)`, конструкторов в языке нет. Атрибут `#[derive(Debug)]` генерирует вывод `{:?}`, а `Clone` - метод `clone()`. Без `Clone` копия не создаётся: структура с `String` перемещается, как сама `String`. Функция `best` возвращает `Option<&Student>`: ссылку на элемент вектора или `None` для пустого вектора; компилятор сам понимает, что ссылка живёт столько же, сколько заимствованный вектор. Охранное условие `Some(current) if current.score >= s.score` оставляет при равенстве первого, поэтому лучшей из «Бориса» и «Веры» остался Борис. Метод `sort_by` принимает замыкание-сравнение; ключи сравнивают через `cmp`. Сортировка устойчива: Борис и Вера с равными баллами сохраняют прежний порядок.

<!-- section: precise -->
## Точные правила

* `Option<T>` - `Some(T)` или `None`; `Result<T, E>` - `Ok(T)` или `Err(E)`. Оба обрабатываются через `match` (все варианты обязательны), `if let`, `while let` или методы.
* Основные методы: `is_some`, `is_none`, `unwrap_or(x)`, `unwrap_or_default()`, `map`, `ok_or(err)` (из `Option` в `Result`), `ok()` (из `Result` в `Option`), `map_err`, `and_then`.
* `unwrap` и `expect` паникуют на `None` и `Err`. Паника завершает поток с сообщением `called ... on a ... value`; используйте их только там, где пустое значение означает ошибку программиста.
* Оператор `?` разворачивает `Ok` и `Some`, а иначе делает ранний `return` ошибки или `None`. Тип ошибки преобразуется через `From`: `String` в `String` проходит как есть, `ParseIntError` в `String` автоматически не превращается (нужен `map_err`).
* `?` разрешён только в функции, возвращающей `Result`/`Option`. В `main` с типом `()` он даёт ошибку компиляции.
* `parse::<T>()` возвращает `Result<T, _>`; разбирается вся строка, пробелы по краям нужно убрать `trim()`; `"12abc"`, `""`, `"+"` и `"1.5"` для `i32` - ошибки; значение вне диапазона типа (`"3000000000"` для `i32`) тоже ошибка, а не переполнение.
* `struct` по умолчанию перемещается; `#[derive(Clone)]` добавляет `clone()`, `Debug` - `{:?}` и `{:#?}`, `Copy` разрешён, если все поля `Copy`. Поля и методы по умолчанию приватны за пределами модуля (в одном файле это незаметно).
* `&self` читает, `&mut self` изменяет, `self` забирает владение. Вызов `x.method()` сам подставляет `&` и `&mut` (автоматическое заимствование).
* Элемент вектора нельзя вынуть по индексу, если тип не `Copy`: `let s = v[0]` для `Student` - ошибка; берут ссылку `&v[0]` или клонируют.
* Паника в основном потоке завершает программу с ненулевым кодом; сообщение идёт в stderr.

<!-- section: mistakes -->
## Типичные ошибки

Сообщения ниже получены настоящим `rustc` 1.99; пути к стандартной библиотеке заменены многоточием.

**1. Использование `Result` как самого значения.**

```rust
fn main() {
    let n: i32 = "5".parse();
    println!("{n}");
}
```

```text
error[E0308]: mismatched types
 --> main.rs:2:18
  |
2 |     let n: i32 = "5".parse();
  |            ---   ^^^^^^^^^^^ expected `i32`, found `Result<_, _>`
  |            |
  |            expected due to this
  |
  = note: expected type `i32`
             found enum `Result<_, _>`
help: consider using `Result::expect` to unwrap the `Result<_, _>` value, panicking if the value is a `Result::Err`
  |
2 |     let n: i32 = "5".parse().expect("REASON");
  |                             +++++++++++++++++
```

`parse` не возвращает число, а возвращает `Result` с числом внутри. Разберите его через `match`, `unwrap_or` или `?`.

**2. Пропущен вариант `None`.**

```rust
fn main() {
    let values = vec![1, 2, 3];
    match values.first() {
        Some(x) => println!("{x}"),
    }
}
```

```text
error[E0004]: non-exhaustive patterns: `None` not covered
   --> main.rs:3:11
    |
  3 |     match values.first() {
    |           ^^^^^^^^^^^^^^ pattern `None` not covered
    |
    = note: the matched value is of type `Option<&i32>`
help: ensure that all possible cases are being handled by adding a match arm with a wildcard pattern or an explicit pattern as shown
    |
  4 ~         Some(x) => println!("{x}"),
  5 ~         None => todo!(),
    |
```

**3. `?` в функции, которая ничего не возвращает.**

```rust
fn main() {
    let n: i32 = "5".parse()?;
    println!("{n}");
}
```

```text
error[E0277]: the `?` operator can only be used in a function that returns `Result` or `Option` (or another type that implements `FromResidual`)
 --> main.rs:2:29
  |
1 | fn main() {
  | --------- this function should return `Result` or `Option` to accept `?`
2 |     let n: i32 = "5".parse()?;
  |                             ^ cannot use the `?` operator in a function that returns `()`
  |
help: consider adding return type
  |
1 ~ fn main() -> Result<(), Box<dyn std::error::Error>> {
2 |     let n: i32 = "5".parse()?;
3 |     println!("{n}");
4 +     Ok(())
  |
```

Либо `main` не используют для разбора и переносят код в функцию с `Result`, либо обрабатывают результат через `match`.

**4. Нет `#[derive(Debug)]`.**

```rust
struct Point {
    x: i32,
    y: i32,
}

fn main() {
    let p = Point { x: 1, y: 2 };
    println!("{:?}", p);
}
```

```text
error[E0277]: `Point` doesn't implement `Debug`
 --> main.rs:8:22
  |
8 |     println!("{:?}", p);
  |               ----   ^ `Point` cannot be formatted using `{:?}` because it doesn't implement `Debug`
  |               |
  |               required by this formatting parameter
  |
  = help: the trait `Debug` is not implemented for `Point`
help: consider annotating `Point` with `#[derive(Debug)]`
  |
1 + #[derive(Debug)]
2 | struct Point {
  |
```

Добавьте `#[derive(Debug)]` над структурой.

**5. Перемещение элемента из вектора.**

```rust
struct Item {
    name: String,
}

fn main() {
    let items = vec![Item { name: String::from("a") }];
    let first = items[0];
    println!("{}", first.name);
}
```

```text
error[E0507]: cannot move out of index of `Vec<Item>`
 --> main.rs:7:17
  |
7 |     let first = items[0];
  |                 ^^^^^^^^ move occurs because value has type `Item`, which does not implement the `Copy` trait
  |
note: if `Item` implemented `Clone`, you could clone the value
 --> main.rs:1:1
  |
1 | struct Item {
  | ^^^^^^^^^^^ consider implementing `Clone` for this type
...
7 |     let first = items[0];
  |                 -------- you could clone this value
help: consider borrowing here
  |
7 |     let first = &items[0];
  |                 +
```

Возьмите ссылку (`&items[0]`) или клонируйте элемент, если для типа выведен `Clone`.

**6. Изменение поля в методе с `&self`.**

```rust
struct Counter {
    value: i32,
}

impl Counter {
    fn bump(&self) {
        self.value += 1;
    }
}

fn main() {
    let c = Counter { value: 0 };
    c.bump();
}
```

```text
error[E0594]: cannot assign to `self.value`, which is behind a `&` reference
 --> main.rs:7:9
  |
7 |         self.value += 1;
  |         ^^^^^^^^^^^^^^^ `self` is a `&` reference, so it cannot be written to
  |
help: consider changing this to be a mutable reference
  |
6 |     fn bump(&mut self) {
  |              +++
```

Метод, который меняет поля, объявляют с `&mut self`, а переменную - с `let mut`.

**7. Паника `unwrap` на пользовательском вводе.** Пример 2 показывает сообщение «called Result::unwrap() on an Err value»: программа упала на первой же опечатке. В программах, которые читают данные, заменяйте `unwrap` на `match`.

**8. Точка с запятой после результата.** Функция `fn add(a: i32, b: i32) -> i32 { a + b; }` не компилируется: `a + b;` - инструкция, и тело возвращает `()`.

<!-- section: summary -->
## Итог

Вы знаете, как Rust выражает отсутствие значения и ошибки: `Option` вместо `null`, `Result` вместо исключений, `match` и `?` для их обработки, `unwrap` как осознанная паника. Структуры задаются через `struct` и `impl`, методы получают `&self` или `&mut self`. Дальше начинается курс первых шагов: [Как работает Rust](lesson:rust.first-steps.how-rust-runs). Для подробностей: [Option и Result](lesson:rust.from-zero.option-and-result), [Ошибки](lesson:rust.from-zero.errors), [Первая структура](lesson:rust.from-zero.first-struct).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* `Option<T>` заменяет `null` и `nullable`-типы (по смыслу ближе к nullable-типам Kotlin и Swift и к `Optional` из Java, но без значения `null` вообще). `Result<T, E>` заменяет исключения; ближайший аналог - возврат пары `(значение, err)` в Go, только компилятор не позволяет проигнорировать ошибку.
* Оператор `?` - аналог распространения исключения, но явный в тексте и в сигнатуре: видно, какие строки могут вернуть ошибку. Он ближе к `if err != nil { return err }` из Go, записанному одним символом.
* Исключений и `try/catch` нет. Паника (`panic!`, `unwrap`, индекс за границей, деление на ноль) завершает поток и сообщает о баге; перехватывать её как обычную ошибку не принято.
* `struct` + `impl` - аналог класса без наследования: данные и методы разделены, конструктора нет (по соглашению `new`), поля инициализируются все сразу, значения по умолчанию - через `Default`.
* `#[derive(...)]` генерирует реализации стандартных трейтов (`Debug`, `Clone`, `PartialEq`, `Default`), аналог `@Data` из Lombok или автогенерируемых методов `data class` Kotlin.
* Структура - значение: присваивание перемещает её (а не копирует ссылку, как в Java), копия получается через `clone()` или `Copy`.
* Перечисления в Rust несут данные (суммирующие типы): `Option` и `Result` - обычные `enum`; собственные перечисления и трейты разбираются в курсе «Rust: основы».
