<!-- section: simple -->
## Данные, которые знают свою форму

**Структура** (`struct`) объединяет поля в один тип: «и то, и другое». **Перечисление** (`enum`) описывает «или то, или другое» и, в отличие от перечислений в C, каждый вариант может нести свои данные. Вместе они позволяют записать модель предметной области так, чтобы невозможные состояния вообще нельзя было создать.

```rust run
#[derive(Debug)]
enum Shape {
    Circle(f64),               // один радиус
    Rect { w: f64, h: f64 },   // именованные поля
    Point,                     // без данных
}

fn area(s: &Shape) -> f64 {
    match s {
        Shape::Circle(r) => 3.0 * r * r,
        Shape::Rect { w, h } => w * h,
        Shape::Point => 0.0,
    }
}

fn main() {
    let shapes = [Shape::Circle(2.0), Shape::Rect { w: 2.0, h: 3.5 }, Shape::Point];
    for s in &shapes {
        println!("{:?} -> {}", s, area(s));
    }
}
```
```output
Circle(2.0) -> 12
Rect { w: 2.0, h: 3.5 } -> 7
Point -> 0
```

Оператор `match` проверяет значение по образцам и **обязан** покрыть все варианты. Если добавить вариант в `enum`, компилятор покажет каждый `match`, который нужно обновить. Это одно из главных преимуществ Rust перед «строками-кодами» и флагами.

Самые важные перечисления стандартной библиотеки - `Option<T>` (значение есть или нет) и `Result<T, E>` (успех или ошибка). Вместо `null` у вас `None`, и компилятор не даст забыть его обработать.

<!-- section: precise -->
## Структуры, перечисления и образцы точно

**Виды структур.** С именованными полями (`struct User { name: String }`), кортежные (`struct Meters(f64);` - так делают «новые типы», newtype) и единичные (`struct Marker;`). Методы пишут в блоке `impl`; первый параметр `self`, `&self` или `&mut self` определяет, забирает ли метод владение, читает или меняет. Функции без `self` (`Point::new`) вызываются через `::`.

**Синтаксис обновления.** `User { email: new, ..old }` копирует/перемещает остальные поля из `old`. Если среди них есть не-`Copy`, `old` после этого частично перемещён.

**Размер enum.** Значение `enum` хранит метку варианта и данные самого большого варианта (с учётом выравнивания). Компилятор применяет оптимизацию «ниши»: `Option<&T>`, `Option<Box<T>>` и `Option<NonZeroU32>` занимают столько же места, сколько сам `T`, потому что `None` кодируется недопустимым значением (нулевым указателем).

**Образцы.** Образцы встречаются в `match`, `let`, параметрах функций, `if let`, `while let`, `for`. Основные формы:

* литералы и диапазоны: `0`, `'a'..='z'`, `1..=9`;
* привязка: `n`; привязка с проверкой: `n @ 1..=9`;
* деструктуризация: `Point { x, y }`, `(a, b)`, `[first, .., last]`, `[head, rest @ ..]`;
* альтернативы: `1 | 2 | 3`;
* защитное условие: `Some(n) if n > 0`;
* подстановка `_` и `..`.

**Режимы привязки.** Если сопоставлять `&T` с образцом без `&`, поля привязываются как ссылки (`match &opt { Some(s) => ... }` даёт `s: &String`). Поэтому `match` по ссылке не перемещает данные. Сопоставление `Option<String>` по значению перемещает строку в образец.

**Исчерпываемость.** Компилятор проверяет, что образцы покрывают все значения (иначе `E0004`). Образец `_` покрывает остаток, но он же скрывает новые варианты в будущем; для собственных перечислений предпочитайте перечислять варианты явно. `#[non_exhaustive]` у чужого `enum` требует `_` намеренно.

**Опровержимые и неопровержимые образцы.** `let (a, b) = pair;` допускает только неопровержимый образец. Для образцов, которые могут не подойти, есть `if let`, `while let` и `let ... else`.

**let-else.** `let Some(x) = opt else { return; };` - привязывает при совпадении, а иначе блок `else` обязан расходиться (`return`, `break`, `continue`, `panic!`). Удобно для «ранних выходов» без вложенности.

**Option.** Часто используемые методы: `map`, `and_then` (цепочка `Option`-возвращающих шагов), `filter`, `unwrap_or`, `unwrap_or_else`, `unwrap_or_default`, `ok_or`/`ok_or_else` (в `Result`), `is_some_and`, `zip`, `as_ref`, `as_deref`, `take`, `replace`, `?` внутри функций, возвращающих `Option`. `unwrap` и `expect` паникуют на `None`, о дисциплине их применения - в [уроке об обработке ошибок](lesson:rust.fundamentals.error-handling).

<!-- section: examples -->
## Примеры

**Пример 1. Структуры, методы, обновление.**

```rust run
#[derive(Debug, Clone, PartialEq)]
struct Config {
    name: String,
    retries: u32,
    verbose: bool,
}

impl Config {
    fn new(name: &str) -> Self {
        Config { name: name.to_string(), retries: 3, verbose: false }
    }
    fn with_retries(self, retries: u32) -> Self {   // self по значению: цепочка
        Config { retries, ..self }
    }
    fn describe(&self) -> String {
        format!("{} (retries={}, verbose={})", self.name, self.retries, self.verbose)
    }
}

fn main() {
    let base = Config::new("api");
    let fast = base.clone().with_retries(1);
    println!("{}", base.describe());
    println!("{}", fast.describe());
    println!("{}", base == fast);
}
```
```output
api (retries=3, verbose=false)
api (retries=1, verbose=false)
false
```

**Пример 2. Перечисление с данными и методы на нём.**

```rust run
#[derive(Debug)]
enum Event {
    Click { x: i32, y: i32 },
    Key(char),
    Resize(u32, u32),
    Quit,
}

impl Event {
    fn describe(&self) -> String {
        match self {
            Event::Click { x, y } if *x < 0 || *y < 0 => "клик вне окна".to_string(),
            Event::Click { x, y } => format!("клик в ({x}, {y})"),
            Event::Key(c @ 'a'..='z') => format!("строчная {c}"),
            Event::Key(c) => format!("другая клавиша {c:?}"),
            Event::Resize(w, h) => format!("размер {w}x{h}"),
            Event::Quit => "выход".to_string(),
        }
    }
}

fn main() {
    let events = [Event::Click { x: 3, y: 4 }, Event::Click { x: -1, y: 0 }, Event::Key('q'),
                  Event::Key('Q'), Event::Resize(80, 24), Event::Quit];
    for e in &events {
        println!("{}", e.describe());
    }
}
```
```output
клик в (3, 4)
клик вне окна
строчная q
другая клавиша 'Q'
размер 80x24
выход
```

**Пример 3. `if let`, `let-else`, `while let`, `matches!`.**

```rust run
fn parse_pair(s: &str) -> Option<(i32, i32)> {
    let (a, b) = s.split_once(',')?;                      // ? на Option
    let (Ok(a), Ok(b)) = (a.trim().parse(), b.trim().parse()) else {
        return None;                                      // let-else: ранний выход
    };
    Some((a, b))
}

fn main() {
    if let Some((a, b)) = parse_pair("3, 4") {
        println!("сумма {}", a + b);
    }
    println!("{:?} {:?}", parse_pair("x,1"), parse_pair("nocomma"));

    let mut stack = vec![1, 2, 3];
    while let Some(top) = stack.pop() {
        print!("{top} ");
    }
    println!();

    let c = 'g';
    println!("{}", matches!(c, 'a'..='f' | 'x'));
}
```
```output
сумма 7
None None
3 2 1 
false
```

**Пример 4. Образцы: срезы, вложенность, привязка `@`.**

```rust run
fn classify(v: &[i32]) -> String {
    match v {
        [] => "пусто".to_string(),
        [x] => format!("один: {x}"),
        [first, .., last] if first == last => format!("замкнуто на {first}"),
        [first, rest @ ..] => format!("начало {first}, ещё {}", rest.len()),
    }
}

fn bucket(n: u32) -> &'static str {
    match n {
        0 => "ноль",
        d @ 1..=9 if d % 2 == 1 => "нечётная цифра",
        1..=9 => "чётная цифра",
        10 | 20 | 30 => "круглое",
        _ => "много",
    }
}

fn main() {
    for v in [&[][..], &[7], &[1, 2, 1], &[1, 2, 3]] {
        println!("{}", classify(v));
    }
    for n in [0, 3, 4, 20, 99] {
        println!("{n}: {}", bucket(n));
    }
}
```
```output
пусто
один: 7
замкнуто на 1
начало 1, ещё 2
0: ноль
3: нечётная цифра
4: чётная цифра
20: круглое
99: много
```

**Пример 5. Комбинаторы `Option`.**

```rust run
fn find_user(id: u32) -> Option<&'static str> {
    match id {
        1 => Some("ada"),
        2 => Some("bob"),
        _ => None,
    }
}

fn main() {
    let name_len = find_user(1).map(|n| n.len());                  // Some(3)
    let upper = find_user(2).map(str::to_uppercase);               // Some("BOB")
    let missing = find_user(9).map(|n| n.len()).unwrap_or(0);      // 0
    let chain = find_user(1).and_then(|n| n.chars().next());       // Some('a')
    let long = find_user(1).filter(|n| n.len() > 3);               // None
    let pair = find_user(1).zip(find_user(2));                     // Some(("ada", "bob"))
    let r: Result<&str, String> = find_user(7).ok_or_else(|| "нет пользователя 7".to_string());
    println!("{name_len:?} {upper:?} {missing} {chain:?} {long:?} {pair:?}");
    println!("{r:?}");

    let mut slot = Some(String::from("занято"));
    let taken = slot.take();                // слот опустел, значение у нас
    println!("{taken:?} {slot:?}");

    let name: Option<String> = Some("Ada".into());
    let view: Option<&str> = name.as_deref();   // Option<String> -> Option<&str> без перемещения
    println!("{view:?} {:?}", name);
}
```
```output
Some(3) Some("BOB") 0 Some('a') None Some(("ada", "bob"))
Err("нет пользователя 7")
Some("занято") None
Some("Ada") Some("Ada")
```

**Пример 6. Что не соберётся: нет варианта в `match`.**

```
error[E0004]: non-exhaustive patterns: `&Shape::Point` not covered
```

Исправление: добавить ветку `Shape::Point => ...` либо, если остальные варианты действительно равноценны, `_ => ...`.

<!-- section: mistakes -->
## Типичные ошибки

* **Использовать `_` в `match` по своему `enum`.** Добавленный позже вариант будет молча попадать в `_`; перечисляйте варианты явно.
* **Хранить состояние в нескольких полях вместо enum.** Конструкции вроде `is_open: bool, error: Option<String>` допускают невозможные комбинации; `enum State { Open, Failed(String) }` - нет.
* **Забыть, что `match` по значению перемещает.** `match opt { Some(s) => ..., None => ... }` забирает строку из `opt`; для чтения используйте `match &opt` или `opt.as_ref()`.
* **Бесконечные `unwrap()` вместо комбинаторов.** `opt.map(...).unwrap_or(...)` и `?` выразительнее и не паникуют.
* **`if let Some(x) = opt { ... } else { return; }` с пирамидой вложенности.** Применяйте `let Some(x) = opt else { return; };`.
* **Ожидать, что `let-else` может «провалиться дальше».** Блок `else` обязан расходиться (`return`, `continue`, `break`, `panic!`).
* **Сравнивать `enum` оператором `==` без `PartialEq`.** Добавьте `#[derive(PartialEq)]` либо используйте `matches!`.

<!-- section: summary -->
## Итоги

* `struct` - «и», `enum` - «или», варианты `enum` могут нести данные; `match` обязан быть исчерпывающим (иначе `E0004`).
* Образцы: литералы, диапазоны, `@`, защитные условия, срезы `[first, .., last]`, вложенная деструктуризация.
* `if let`, `while let` и `let ... else` - короткие формы для одного опровержимого образца.
* `Option` вместо `null`: `map`, `and_then`, `filter`, `unwrap_or`, `ok_or`, `take`, `as_deref` и `?` избавляют от большинства `unwrap`.
* Сопоставление по ссылке не перемещает данные, по значению - перемещает.

Сравните с [структурами, методами и встраиванием в Go](lesson:go.fundamentals.structs-methods-embedding), где вместо `enum` с данными используют интерфейсы и `switch` по типу без проверки исчерпываемости.
