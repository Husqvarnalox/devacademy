<!-- section: simple -->
## Трейты: общее поведение разных типов

**Трейт** (trait) описывает набор методов, которые тип обязуется реализовать. Это аналог интерфейса, но с важным отличием: реализовать трейт для типа можно отдельно от определения типа, а выбор реализации во многих случаях происходит на этапе компиляции.

**Обобщения** (generics) позволяют писать код один раз для многих типов. Ограничение (**trait bound**) говорит, что именно нужно от типа: `T: Display + PartialOrd`.

```rust run
use std::fmt::Display;

trait Shape {
    fn area(&self) -> f64;
    fn name(&self) -> String {          // метод по умолчанию
        "фигура".to_string()
    }
}

struct Circle(f64);
struct Square(f64);

impl Shape for Circle {
    fn area(&self) -> f64 { 3.0 * self.0 * self.0 }
    fn name(&self) -> String { "круг".to_string() }
}
impl Shape for Square {
    fn area(&self) -> f64 { self.0 * self.0 }       // name() берётся по умолчанию
}

fn print_area<S: Shape>(s: &S) {                     // статическая диспетчеризация
    println!("{}: {}", s.name(), s.area());
}

fn larger<T: PartialOrd + Display>(a: T, b: T) -> String {
    if a >= b { format!("{a}") } else { format!("{b}") }
}

fn main() {
    print_area(&Circle(2.0));
    print_area(&Square(3.0));
    println!("{} {}", larger(3, 8), larger("яблоко", "арбуз"));
}
```
```output
круг: 12
фигура: 9
8 яблоко
```

Для функции `print_area::<Circle>` компилятор генерирует отдельную копию кода (**мономорфизация**): вызов метода прямой, без накладных расходов, но размер программы растёт. Если нужно хранить в одном списке разные типы, используют **трейт-объекты** `dyn Shape` с динамической диспетчеризацией через таблицу виртуальных методов.

<!-- section: precise -->
## Трейты и обобщения точно

**Определение и реализация.** `trait Name { fn m(&self); fn d(&self) { ... } }`; `impl Name for Type { ... }`. Метод по умолчанию можно переопределить. Трейт может требовать другой трейт (`trait Pet: Display`, «суперtrait»).

**Правило сирот (orphan rule).** Реализовать трейт для типа можно, только если локален либо трейт, либо тип. Нельзя написать `impl Display for Vec<i32>` (ошибка `E0117`); обходят это «новым типом» `struct Wrapper(Vec<i32>);`.

**Ограничения.** Три равнозначных записи: `fn f<T: A + B>(x: T)`, `fn f<T>(x: T) where T: A + B` и `fn f(x: impl A + B)`. `where` удобен для длинных ограничений. В теле обобщённой функции доступны только методы, обещанные ограничениями: `x > m` для голого `T` даёт `E0369`, а использование `{}` без `Display` - `E0277`.

**Статическая и динамическая диспетчеризация.**

| | `impl Trait` / `T: Trait` | `dyn Trait` |
|---|---|---|
| выбор метода | при компиляции (мономорфизация) | во время работы (vtable) |
| вызов | прямой, можно встроить (inline) | косвенный через указатель |
| размер кода | растёт с числом типов | один экземпляр функции |
| разнородные значения в `Vec` | нет | да: `Vec<Box<dyn Trait>>` |
| размер ссылки | `&T` - один указатель | `&dyn Trait` - «толстый» указатель из двух слов |

`impl Trait` в аргументе - сокращение обобщения; в **возвращаемом** типе он означает «какой-то один конкретный тип, реализующий трейт» (скрывает тип, например, замыкание). Вернуть из одной функции то `Circle`, то `Square` через `impl Shape` нельзя - для этого нужен `Box<dyn Shape>`.

**Совместимость с `dyn` (dyn compatibility, раньше «object safety»).** Трейт можно использовать как `dyn`, если его методы не возвращают `Self`, не имеют собственных обобщённых параметров типа (без `where Self: Sized`) и т. п. Иначе `E0038`: *the trait is not dyn compatible*.

**Derive.** Атрибут `#[derive(...)]` генерирует реализации: `Debug`, `Clone`, `Copy`, `PartialEq`, `Eq`, `PartialOrd`, `Ord`, `Hash`, `Default`. Для структур сравнение по полям в порядке объявления; для `enum` - по порядку вариантов. `Display` вручную: `impl fmt::Display for T { fn fmt(&self, f: &mut fmt::Formatter) -> fmt::Result { write!(f, ...) } }`; тип с `Display` получает `to_string()`. `Debug` нужен для `{:?}` и `unwrap` на `Result`.

**Операторы - тоже трейты** из `std::ops`: `Add`, `Sub`, `Mul`, `Neg`, `Index`, `AddAssign` и т. д. `a + b` раскрывается в `Add::add(a, b)`, при этом операнды перемещаются (для `Copy` типов это незаметно). Выход `Output` задаётся ассоциированным типом.

**Ассоциированные типы.** `trait Iterator { type Item; fn next(&mut self) -> Option<Self::Item>; }` - тип определяется реализацией, а не параметром вызова. Обобщённый параметр трейта (`Add<Rhs>`) допускает несколько реализаций для одного типа, ассоциированный тип - одну.

**Универсальные реализации (blanket impls).** `impl<T: Display> Describe for T { ... }` реализует трейт сразу для всех типов с `Display`; так устроен `ToString`.

**Время жизни и трейты.** `Box<dyn Trait>` означает `Box<dyn Trait + 'static>`: трейт-объект не содержит коротких ссылок, пока вы не напишете `Box<dyn Trait + 'a>`.

<!-- section: examples -->
## Примеры

**Пример 1. Ограничения и `where`; разные записи равнозначны.**

```rust run
use std::fmt::{Debug, Display};

trait Summary {
    fn author(&self) -> String;
    fn summarize(&self) -> String {
        format!("(читать далее от {}...)", self.author())
    }
}

struct Post { user: String, text: String }
struct Tweet { user: String }

impl Summary for Post {
    fn author(&self) -> String { self.user.clone() }
    fn summarize(&self) -> String { format!("{}: {}", self.user, self.text) }
}
impl Summary for Tweet {
    fn author(&self) -> String { format!("@{}", self.user) }
}

fn notify(item: &impl Summary) -> String {
    format!("Новое! {}", item.summarize())
}

fn show_both<A, B>(a: A, b: B) -> String
where
    A: Display,
    B: Debug,
{
    format!("{a} / {b:?}")
}

fn main() {
    let p = Post { user: "ada".into(), text: "трейты".into() };
    let t = Tweet { user: "bob".into() };
    println!("{}", notify(&p));
    println!("{}", notify(&t));
    println!("{}", show_both("текст", vec![1, 2]));
}
```
```output
Новое! ada: трейты
Новое! (читать далее от @bob...)
текст / [1, 2]
```

**Пример 2. Статическая и динамическая диспетчеризация.**

```rust run
use std::mem::size_of;

trait Animal {
    fn noise(&self) -> &'static str;
}
struct Dog;
struct Cat;
impl Animal for Dog { fn noise(&self) -> &'static str { "гав" } }
impl Animal for Cat { fn noise(&self) -> &'static str { "мяу" } }

fn static_call<A: Animal>(a: &A) -> &'static str { a.noise() }   // копия для Dog и для Cat
fn dynamic_call(a: &dyn Animal) -> &'static str { a.noise() }    // одна функция, vtable

fn main() {
    println!("{} {}", static_call(&Dog), static_call(&Cat));
    let zoo: Vec<Box<dyn Animal>> = vec![Box::new(Dog), Box::new(Cat), Box::new(Dog)];
    let all: Vec<&str> = zoo.iter().map(|a| dynamic_call(a.as_ref())).collect();
    println!("{}", all.join(" "));
    println!("&Dog = {} байт, &dyn Animal = {} байт", size_of::<&Dog>(), size_of::<&dyn Animal>());
}
```
```output
гав мяу
гав мяу гав
&Dog = 8 байт, &dyn Animal = 16 байт
```

**Пример 3. `derive`, `Display` и сортировка.**

```rust run
use std::fmt;

#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord, Hash, Default)]
struct Version {
    major: u32,
    minor: u32,
    patch: u32,
}

impl fmt::Display for Version {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}.{}.{}", self.major, self.minor, self.patch)
    }
}

fn main() {
    let mut vs = vec![
        Version { major: 1, minor: 10, patch: 0 },
        Version { major: 1, minor: 2, patch: 5 },
        Version { major: 0, minor: 9, patch: 9 },
        Version::default(),
    ];
    vs.sort();                                   // порядок по полям сверху вниз
    let shown: Vec<String> = vs.iter().map(|v| v.to_string()).collect();
    println!("{}", shown.join(" < "));
    println!("{:?}", vs.iter().max());
}
```
```output
0.0.0 < 0.9.9 < 1.2.5 < 1.10.0
Some(Version { major: 1, minor: 10, patch: 0 })
```

**Пример 4. Операторы как трейты.**

```rust run
use std::ops::{Add, AddAssign, Index, Mul, Neg, Sub};

#[derive(Debug, Clone, Copy, PartialEq)]
struct Vec2 { x: f64, y: f64 }

impl Add for Vec2 {
    type Output = Vec2;
    fn add(self, o: Vec2) -> Vec2 { Vec2 { x: self.x + o.x, y: self.y + o.y } }
}
impl Sub for Vec2 {
    type Output = Vec2;
    fn sub(self, o: Vec2) -> Vec2 { Vec2 { x: self.x - o.x, y: self.y - o.y } }
}
impl Mul<f64> for Vec2 {
    type Output = Vec2;
    fn mul(self, k: f64) -> Vec2 { Vec2 { x: self.x * k, y: self.y * k } }
}
impl Neg for Vec2 {
    type Output = Vec2;
    fn neg(self) -> Vec2 { Vec2 { x: -self.x, y: -self.y } }
}
impl AddAssign for Vec2 {
    fn add_assign(&mut self, o: Vec2) { self.x += o.x; self.y += o.y; }
}
impl Index<usize> for Vec2 {
    type Output = f64;
    fn index(&self, i: usize) -> &f64 { if i == 0 { &self.x } else { &self.y } }
}

fn main() {
    let a = Vec2 { x: 1.0, y: 2.0 };
    let b = Vec2 { x: 0.5, y: -1.0 };
    let mut c = (a + b) * 2.0 - -a;
    c += Vec2 { x: 1.0, y: 1.0 };
    println!("{:?} {} {}", c, c[0], c[1]);
}
```
```output
Vec2 { x: 5.0, y: 5.0 } 5 5
```

**Пример 5. Обобщённая структура, ассоциированный тип и blanket impl.**

```rust run
use std::fmt::Display;

struct Stack<T> {
    items: Vec<T>,
}

impl<T> Stack<T> {
    fn new() -> Self { Stack { items: Vec::new() } }
    fn push(&mut self, x: T) { self.items.push(x); }
    fn pop(&mut self) -> Option<T> { self.items.pop() }
}

impl<T: Display> Stack<T> {                        // метод только для T: Display
    fn render(&self) -> String {
        self.items.iter().map(|x| x.to_string()).collect::<Vec<_>>().join(",")
    }
}

struct Countdown(u32);
impl Iterator for Countdown {
    type Item = u32;                               // ассоциированный тип
    fn next(&mut self) -> Option<u32> {
        if self.0 == 0 { None } else { self.0 -= 1; Some(self.0 + 1) }
    }
}

trait Describe { fn describe(&self) -> String; }
impl<T: Display> Describe for T {                  // blanket impl
    fn describe(&self) -> String { format!("<{self}>") }
}

fn main() {
    let mut s = Stack::new();
    s.push(1); s.push(2); s.push(3);
    println!("{} {:?}", s.render(), s.pop());
    println!("{:?}", Countdown(3).collect::<Vec<_>>());
    println!("{} {}", 42.describe(), "hi".describe());
}
```
```output
1,2,3 Some(3)
[3, 2, 1]
<42> <hi>
```

**Пример 6. `impl Trait` в возвращаемом типе и `Box<dyn Fn>`.**

```rust run
fn adder(n: i32) -> impl Fn(i32) -> i32 {          // один конкретный тип замыкания
    move |x| x + n
}

fn pick(kind: &str) -> Box<dyn Fn(i32) -> i32> {   // разные замыкания - нужен Box<dyn>
    match kind {
        "double" => Box::new(|x| x * 2),
        "square" => Box::new(|x| x * x),
        _ => Box::new(|x| x),
    }
}

fn main() {
    let add5 = adder(5);
    println!("{}", add5(10));
    for k in ["double", "square", "id"] {
        println!("{k}: {}", pick(k)(7));
    }
}
```
```output
15
double: 14
square: 49
id: 7
```

Ошибки компилятора (`rustc 1.99`), которые вы встретите чаще всего:

```
error[E0369]: binary operation `>` cannot be applied to type `&T`        // нет ограничения PartialOrd
error[E0277]: `P` doesn't implement `std::fmt::Display`                  // println!("{}", P)
error[E0038]: the trait `Cloner` is not dyn compatible                    // метод возвращает Self
error[E0117]: only traits defined in the current crate can be implemented for types defined outside of the crate
```

<!-- section: mistakes -->
## Типичные ошибки

* **Забыть ограничение у обобщённого параметра.** Без `T: PartialOrd` нельзя сравнивать (`E0369`), без `T: Display` - печатать (`E0277`). Читайте подсказку компилятора: она предлагает нужное ограничение.
* **Пытаться вернуть разные типы через `impl Trait`.** Один `impl Trait` - один тип; для разных нужен `Box<dyn Trait>` или `enum`.
* **Использовать `dyn Trait` там, где достаточно обобщения.** Динамическая диспетчеризация нужна для разнородных коллекций и плагинов; для остального обобщение быстрее.
* **Пытаться создать `dyn Trait` из трейта с методом `fn dup(&self) -> Self`** (`E0038`). Уберите `Self` из сигнатуры, добавьте `where Self: Sized` или используйте обобщение.
* **Писать `impl ForeignTrait for ForeignType`** (`E0117`). Заверните тип в свой `struct`.
* **Выводить `PartialOrd` для структуры с неожиданным порядком полей.** Сравнение лексикографическое, в порядке объявления.
* **Реализовывать `Eq`/`Hash` непоследовательно.** Если `a == b`, то и хэши должны совпадать; проще использовать `derive` для обоих.
* **Перегружать операторы ради красоты.** Оператор должен делать то, что от него ждут; иначе используйте именованный метод.

<!-- section: summary -->
## Итоги

* Трейт - общее поведение; методы по умолчанию и суперtrait'ы сокращают дублирование.
* Ограничения `T: A + B`, `where` и `impl Trait` равнозначны по смыслу; в теле можно использовать только обещанное ограничениями.
* Обобщения компилируются в отдельные копии (быстро, больше кода), `dyn Trait` вызывает через vtable (гибко, косвенный вызов, толстый указатель).
* `derive` генерирует `Debug`, `Clone`, `PartialEq`, `Ord`, `Hash`, `Default`; `Display` и операторы (`std::ops`) пишутся вручную.
* Правило сирот запрещает чужой трейт для чужого типа; обход - «новый тип».

Сравните с [интерфейсами в Go](lesson:go.fundamentals.interfaces): там реализация неявная и интерфейс всегда динамический, а в Rust реализация явная и выбор между статической и динамической диспетчеризацией принимаете вы.
