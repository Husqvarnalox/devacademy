<!-- section: simple -->
## Зачем нужна структура

Пока данные одного студента лежат в двух переменных (`name` и `score`), всё просто. Но стоит завести десять студентов, передавать их в функции, класть в вектор - и становится непонятно, какая оценка чья. Функция с параметрами `(name1, score1, name2, score2)` легко путает порядок.

**Структура** (`struct`) - это ваш собственный тип, который объединяет связанные значения под одним именем. Каждое значение внутри называется **полем** и имеет имя и тип. Теперь «студент» - один объект с полями `name` и `score`, и его можно передавать, хранить в `Vec`, возвращать из функций.

## Одна новая идея: данные отдельно, поведение в `impl`

Сначала объявляется структура - какие у неё поля. Затем блоком `impl Student { ... }` ей добавляются функции:

* **ассоциированная функция** `new`, которая создаёт значение: вызывается `Student::new(...)`;
* **методы** - функции, у которых первым параметром идёт `self`: они вызываются через точку, `s.describe()`, и работают с полями `self.name`, `self.score`.

Привычка из других языков: структура с `impl` заменяет класс; в Rust нет наследования.

<!-- section: examples -->
## Пример 1. Объявление, создание, поля

```rust run
struct Student {
    name: String,
    score: i32,
}

fn main() {
    let mut s = Student {
        name: String::from("Анна"),
        score: 70,
    };
    println!("{} {}", s.name, s.score);
    s.score += 15;
    println!("{} {}", s.name, s.score);
}
```
```output
Анна 70
Анна 85
```

* `struct Student { name: String, score: i32 }` объявляет тип: поля через запятую, у каждого имя и тип.
* `Student { name: ..., score: ... }` создаёт значение: нужно указать **все** поля.
* `s.name`, `s.score` читают поля через точку; `s.score += 15` меняет поле, поэтому переменная объявлена `let mut`.
* Поле `name` типа `String` принадлежит структуре: текст живёт, пока жива она.

**Попробуйте.** Добавьте третье поле `group: i32` и выведите его. Что скажет компилятор, пока вы не добавите его в создание значения?

## Пример 2. `impl`: `new` и методы

```rust run
struct Student {
    name: String,
    score: i32,
}

impl Student {
    fn new(name: &str, score: i32) -> Student {
        Student {
            name: String::from(name),
            score,
        }
    }

    fn is_passed(&self) -> bool {
        self.score >= 60
    }

    fn add_bonus(&mut self, bonus: i32) {
        self.score += bonus;
    }

    fn describe(&self) -> String {
        format!("{}: {}", self.name, self.score)
    }
}

fn main() {
    let mut s = Student::new("Борис", 55);
    println!("{} {}", s.describe(), s.is_passed());
    s.add_bonus(10);
    println!("{} {}", s.describe(), s.is_passed());
}
```
```output
Борис: 55 false
Борис: 65 true
```

* `Student::new("Борис", 55)` - вызов функции с двойным двоеточием. В `new` нет `self`, это просто «фабрика» значения. Название `new` - соглашение, а не ключевое слово.
* `Student { name: String::from(name), score }` - если имя переменной совпадает с именем поля, можно писать короче: `score` вместо `score: score`.
* `fn is_passed(&self) -> bool` - метод, который только **читает** структуру. `&self` - это ссылка на структуру, как `&str` или `&Vec<i32>` в параметрах из прошлых уроков. Вызывается `s.is_passed()`, и `&` писать не нужно.
* `fn add_bonus(&mut self, bonus: i32)` - метод, который **меняет** структуру; нужен `&mut self`, а вызывающая переменная должна быть `mut`.
* Внутри методов поля берутся через `self.`: `self.score`.

## Пример 3. `#[derive(Debug)]` и вывод через `{:?}`

```rust run
#[derive(Debug)]
struct Point {
    x: i32,
    y: i32,
}

#[derive(Debug)]
struct Student {
    name: String,
    score: i32,
}

fn main() {
    let p = Point { x: 3, y: -1 };
    println!("{:?}", p);
    println!("{p:?}, сумма координат: {}", p.x + p.y);

    let s = Student { name: String::from("Вера"), score: 92 };
    println!("{:#?}", s);
    println!("{} набрала {}", s.name, s.score);

    let mut group: Vec<Student> = Vec::new();
    group.push(s);
    group.push(Student { name: String::from("Глеб"), score: 48 });
    println!("{:?}", group);
}
```
```output
Point { x: 3, y: -1 }
Point { x: 3, y: -1 }, сумма координат: 2
Student {
    name: "Вера",
    score: 92,
}
Вера набрала 92
[Student { name: "Вера", score: 92 }, Student { name: "Глеб", score: 48 }]
```

* Строка `#[derive(Debug)]` перед структурой просит компилятор самому написать код отладочного вывода. После этого структуру можно печатать через `{:?}`.
* `{:#?}` - тот же вывод, но по строкам с отступами: удобно для больших структур.
* Вектор структур печатается целиком: `[Student { name: "Вера", score: 92 }, ...]`.
* `{:?}` нужен для отладки. Для вывода пользователю пишут свои строки, например метод `describe`.

## Пример 4. Структуры из ввода

Программа читает ввод, поэтому приведены ввод и вывод.

```rust
struct Student {
    name: String,
    score: i32,
}

fn parse_student(line: &str) -> Result<Student, String> {
    match line.split_once(';') {
        Some((name, score_text)) => match score_text.trim().parse::<i32>() {
            Ok(score) => Ok(Student { name: String::from(name.trim()), score }),
            Err(_) => Err(format!("плохой балл: {}", score_text.trim())),
        },
        None => Err(format!("нет разделителя: {line}")),
    }
}

fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut students: Vec<Student> = Vec::new();
    for line in text.lines() {
        match parse_student(line) {
            Ok(s) => students.push(s),
            Err(msg) => println!("пропущено ({msg})"),
        }
    }
    let mut best = 0;
    for s in &students {
        println!("{}: {}", s.name, s.score);
        if s.score > best {
            best = s.score;
        }
    }
    println!("студентов: {}, лучший балл: {best}", students.len());
}
```

Ввод:

```text
Анна;85
Борис;abc
Вера;50
без точки с запятой
Глеб; 95 
```

Вывод:

```text
пропущено (плохой балл: abc)
пропущено (нет разделителя: без точки с запятой)
Анна: 85
Вера: 50
Глеб: 95
студентов: 3, лучший балл: 95
```

Это знакомая схема из прошлых уроков, но теперь строка превращается не в число, а в структуру. `parse_student` возвращает `Result<Student, String>`: строка либо стала студентом, либо получила описание проблемы. Неверные строки сообщаются и пропускаются, остальные попадают в вектор. Цикл `for s in &students` читает структуры по ссылке, не забирая вектор.

<!-- section: precise -->
## Точные правила

* `struct Имя { поле: Тип, ... }` объявляет тип; имя пишется с заглавной буквы (соглашение), поля - со строчной.
* Значение создаётся `Имя { поле: значение, ... }`: указываются все поля, порядок любой. Запись `поле` вместо `поле: поле` работает, если переменная называется так же.
* Поля читаются `значение.поле`; менять поля можно только у `mut`-переменной.
* `impl Имя { ... }` задаёт функции типа. Функция без `self` - ассоциированная: `Имя::функция(...)`. Функция с `self` - метод: `значение.метод(...)`.
* `&self` - метод читает, `&mut self` - метод меняет, `self` (без ссылки) - метод забирает значение во владение (в курсе не используется).
* Внутри метода к полю обращаются `self.поле`.
* `#[derive(Debug)]` над `struct` разрешает печать `{:?}` и `{:#?}`; без него `{:?}` для структуры не скомпилируется.
* Структуру можно хранить в `Vec<Имя>`, передавать как `&Имя` и возвращать из функций.
* Структура владеет своими полями: `String` внутри неё живёт, пока жива структура.

<!-- section: mistakes -->
## Типичные ошибки

**1. Пропущено поле при создании.**

```rust
struct Student {
    name: String,
    score: i32,
}

fn main() {
    let s = Student { name: String::from("Анна") };
    println!("{}", s.name);
}
```

```text
error[E0063]: missing field `score` in initializer of `Student`
 --> main.rs:7:13
  |
7 |     let s = Student { name: String::from("Анна") };
  |             ^^^^^^^ missing `score`
```

При создании значения нужно задать все поля.

**2. Метод с `&mut self` для неизменяемой переменной.**

```rust
struct Student {
    name: String,
    score: i32,
}

impl Student {
    fn add_bonus(&mut self, bonus: i32) {
        self.score += bonus;
    }
}

fn main() {
    let s = Student { name: String::from("Анна"), score: 70 };
    s.add_bonus(5);
    println!("{} {}", s.name, s.score);
}
```

```text
error[E0596]: cannot borrow `s` as mutable, as it is not declared as mutable
  --> main.rs:14:5
   |
14 |     s.add_bonus(5);
   |     ^ cannot borrow as mutable
   |
help: consider changing this to be mutable
   |
13 |     let mut s = Student { name: String::from("Анна"), score: 70 };
   |         +++
```

Метод меняет структуру, поэтому переменная должна быть `let mut s`.

**3. Печать структуры без `derive`.**

```rust
struct Student {
    name: String,
    score: i32,
}

fn main() {
    let s = Student { name: String::from("Анна"), score: 70 };
    println!("{:?}", s);
}
```

```text
error[E0277]: `Student` doesn't implement `Debug`
 --> main.rs:8:22
  |
8 |     println!("{:?}", s);
  |               ----   ^ `Student` cannot be formatted using `{:?}` because it doesn't implement `Debug`
  |               |
  |               required by this formatting parameter
  |
  = help: the trait `Debug` is not implemented for `Student`
help: consider annotating `Student` with `#[derive(Debug)]`
  |
1 + #[derive(Debug)]
2 | struct Student {
  |
```

Компилятор советует добавить `#[derive(Debug)]` над структурой.

**4. Поле без `self.` в методе.**

```rust
struct Student {
    name: String,
    score: i32,
}

impl Student {
    fn is_passed(&self) -> bool {
        score >= 60
    }
}

fn main() {
    let s = Student { name: String::from("Анна"), score: 70 };
    println!("{} {}", s.name, s.is_passed());
}
```

```text
error[E0425]: cannot find value `score` in this scope
 --> main.rs:8:9
  |
8 |         score >= 60
  |         ^^^^^
  |
help: you might have meant to use the available field
  |
8 |         self.score >= 60
  |         +++++
```

Внутри метода поле нужно указывать как `self.score`. Голое `score` - это отдельная переменная, которой нет.

**5. Забыт `&self` в сигнатуре.**

```rust
struct Student {
    name: String,
    score: i32,
}

impl Student {
    fn is_passed() -> bool {
        true
    }
}

fn main() {
    let s = Student { name: String::from("Анна"), score: 70 };
    println!("{} {}", s.name, s.is_passed());
}
```

```text
error[E0599]: no method named `is_passed` found for struct `Student` in the current scope
  --> main.rs:14:33
   |
 1 | struct Student {
   | -------------- method `is_passed` not found for this struct
...
14 |     println!("{} {}", s.name, s.is_passed());
   |                                 ^^^^^^^^^ this is an associated function, not a method
   |
   = note: found the following associated functions; to be used as methods, functions must have a `self` parameter
note: the candidate is defined in an impl for the type `Student`
  --> main.rs:7:5
   |
 7 |     fn is_passed() -> bool {
   |     ^^^^^^^^^^^^^^^^^^^^^^
help: use associated function syntax instead
   |
14 -     println!("{} {}", s.name, s.is_passed());
14 +     println!("{} {}", s.name, Student::is_passed());
   |
```

Без `self` функция не метод: её нужно вызывать как `Student::is_passed()`, а не через точку. Для метода первым параметром пишите `&self`.

<!-- section: summary -->
## Итоги

* `struct` объединяет связанные поля в один тип; значение создаётся с указанием всех полей.
* `impl Имя { ... }` добавляет функции: `new` (ассоциированная, без `self`) и методы (с `&self` или `&mut self`).
* `&self` читает, `&mut self` меняет; внутри метода поля берут через `self.поле`.
* `#[derive(Debug)]` включает `{:?}` и `{:#?}` для структуры.
* Структуры хранят в `Vec` и создают из строк ввода, пропуская неверные.

Следующий урок: [мини-проект](lesson:rust.from-zero.mini-project): ведомость оценок из строк `имя;балл`, где вы используете всё изученное.

<!-- section: programmer -->
## Для тех, кто уже программирует

* `struct` + `impl` - аналог класса без наследования. Поведение разделяется через трейты (интерфейсы) и композицию; трейты - в `rust-fundamentals`.
* Конструкторов нет: `new` - обычная ассоциированная функция по соглашению. В `impl` внутри блока тип можно называть `Self`: `fn new(...) -> Self`.
* Поля по умолчанию приватны для модуля; `pub` и модули отложены (`rust-fundamentals`). Одиночный файл `main.rs` всё видит.
* `&self`, `&mut self`, `self` - три способа взять владельца, соответствуют «читать», «менять», «потребить». Компилятор проверяет, что изменяемая ссылка эксклюзивна.
* `#[derive(...)]` генерирует реализации трейтов; `Debug` для отладочного вывода, дальше обычно нужны `Clone`, `PartialEq` и другие.
* Структура с `String` не `Copy`: присваивание `let b = a;` перемещает значение, как в уроке про владение.
* Кортежные структуры, единичные структуры и `enum` с данными - в `rust-fundamentals`.
* Размер структуры известен во время компиляции; поля могут быть выровнены, и порядок в памяти компилятор вправе менять.

<!-- section: deep-dive -->
## Как структура лежит в памяти

Это необязательный раздел: для заданий он не нужен.

Структура `Student { name: String, score: i32 }` - это просто поля, расположенные друг за другом. Поле `score` (4 байта) лежит в самой структуре, а поле `name` содержит три числа (указатель, длину, ёмкость), как в уроке про векторы; сам текст находится в куче. Поэтому структура **владеет** текстом: когда она уходит из области видимости, освобождается и строка.

Из этого следуют правила, которые вы уже видели. Передача `Student` в функцию по значению перемещает её вместе с владением текстом, поэтому прежняя переменная становится недоступной; `&Student` передаёт ссылку, и ничего не копируется. Атрибут `derive(Clone)`, о котором расскажет следующий курс, добавляет `.clone()`, который копирует и число, и текст.

Методы не хранятся в структуре. `s.describe()` - это синтаксис для вызова обычной функции `Student::describe(&s)`: компилятор подставляет `&s` на место `self`. Поэтому методы не увеличивают размер значения.
