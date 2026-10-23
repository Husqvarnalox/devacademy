# Трек Rust: структура

Стандарт: [../curriculum-standards.md](../curriculum-standards.md). Эталоны: [go.md](go.md), [javascript.md](javascript.md), ближайшие по устройству компилируемые треки - [cpp.md](cpp.md) и [kotlin.md](kotlin.md). Маршрут: `content/routes/rust.yaml`.

```
rust-from-zero (уровень 0) ──┐
                             ├─> rust-first-steps (rustc, нет рантайма, i64 и переполнение; владение, перемещение, заимствование) ─> rust-fundamentals
rust-for-programmers (1) ────┘
   зачитывает уроки rust-from-zero
rust-skill-check: проверки компетенций
```

## Особенности Rust, которые определили порядок

* **Как запускается программа.** Каждое упражнение - один файл `main.rs` (`entryFile`). Приложение собирает его командой
  `rustc --edition 2021 -O -o prog main.rs` и запускает `./prog` (см. `RUNTIME_ARCHITECTURE.md`; Cargo не используется,
  внешних крейтов нет). Примеры проверены на rustc 1.99. Флаг `-O` включает оптимизацию, поэтому программы работают как
  релизная сборка: переполнение целых **не** вызывает панику, а заворачивается (подробно - `rust-first-steps`, урок 01).
  Код, где переполнение видно уже при компиляции (`let y: i32 = 3000000000;`, `10 / 0` с константами), `rustc` отвергает
  сам: это lint-ошибки (`overflowing_literals`, `unconditional_panic`), их сообщения используются в разборе ошибок.
* **Компилятор строгий, и это подаётся как помощь.** Урок 01 объясняет «исходный код → компилятор → исполняемый файл»
  простыми словами; урок 02 учит читать сообщения `rustc` (адрес `--> main.rs:3:5`, метки под кодом, `help:` с готовым
  исправлением, код `E0425`). Все сообщения в `mistakes` получены запуском настоящего `rustc`, путь `/opt/homebrew/...`
  из длинных сообщений опущен и заменён многоточием.
* **Обёртка минимальна:** `fn main() { ... }` и больше ничего: ни `class`, ни `args`, ни `import`. В уроке 01
  `fn` объясняется как «объявление функции, подробно - урок 16», а `println!` - как «команда печати; `!` значит, что это
  макрос (заготовка, которую компилятор раскрывает в код); подробности потом». Этого достаточно, чтобы не принимать
  на веру ничего, что нужно для упражнения.
* **Ввод - самое тяжёлое место, поэтому он не в уроке 02-03, а в уроке 08**, после переменных, `mut` и строк. Используется
  `std::io::read_to_string(std::io::stdin()).unwrap()` (есть в стандартной библиотеке, проверено): одна строка читает весь
  ввод в `String`, не нужны `mut`, `&mut` и `use std::io::Read`. Дальше `text.lines()` и `lines.next().unwrap()` для
  построчного чтения, `trim()` и (урок 09) `parse()`. `unwrap` объясняется честно: «значение может отсутствовать или быть
  ошибкой; `unwrap` забирает его, а если его нет, останавливает программу с сообщением». Настоящая работа с `Option` и
  `Result` - уроки 18 и 22. До урока 08 stdin пуст, и проверяется фиксированный вывод (по одному открытому и скрытому
  тесту, оба с `stdin: ""`).
* **Чтение до конца ввода** - `for line in text.lines()` (урок 15), поэтому ввод произвольной длины доступен с урока 15
  и не требует `loop`/`break` с `read_line`. Метод `read_line` и `std::io::stdin().lock()` не преподаются.
* **`let` и `let mut`** (урок 04): неизменяемость по умолчанию объясняется как защита («если значение не должно меняться,
  компилятор не даст изменить его случайно»), `mut` - явное разрешение. Затенение (`let x = x.trim()...`) вводится в
  уроке 09, где оно естественно (строка -> число под тем же именем).
* **Числа без неявных преобразований.** `i32` (по умолчанию), `i64`, `f64`, `bool`, `char` - уроки 04-05; `usize` -
  урок 07 (результат `len()`) как «неотрицательное целое для длин и номеров». Остальные размеры (`u8`, `u32`, `i128` ...)
  - одним абзацем «они есть» в уроке 05. `i32 + i64` и `5 == 5.0` - ошибки компиляции (уроки 06, 11); приведение `as` и
  `parse::<T>()` - урок 09. Целочисленное деление и `%` - урок 06; деление на ноль: с константами - ошибка компиляции,
  в рантайме - паника «attempt to divide by zero».
* **Форматирование не зависит от локали.** `{:.2}` всегда печатает точку: `println!("{:.2}", 2.5)` даёт `2.50` на любой
  системе. Это сказано один раз в уроке 10. `println!("{}", 3.0_f64)` печатает `3` (без `.0`) - тоже разобрано в уроке 10;
  `{:?}` печатает `3.0` (отладочный вывод, урок 19).
* **Строки: `&str` и `String`** (урок 07) - «borrowed view» и «owned text» по-русски: `&str` - «заимствованный вид текста, он
  живёт где-то ещё (например, в программе)», `String` - «собственный текст, который можно менять и увеличивать». `&`
  читается «ссылка на»; полные правила - `rust-first-steps` (урок 02). Склейка `+` не преподаётся (она перемещает левую
  строку); строки собирают `push_str` и `format!` (урок 10). `s[0]` не работает (разбирается как ошибка), `len()` считает
  байты (кириллица - по два байта на букву), буквы считает `chars().count()`.
* **Владение вводится в нужном для работы объёме (урок 17):** почему `String`, переданная в функцию, «перемещается»
  (ошибка `E0382` с реальным текстом), как передавать `&str`/`&String`/`&Vec<i32>`/`&mut Vec<i32>`, `.clone()` как честный
  запасной вариант. Не преподаются: три правила владения целиком, времена жизни, `Copy` против `Move` в общем виде, `Box`,
  `Rc` - это `rust-first-steps` (урок 02) и `rust-fundamentals`. Список `Vec<i32>` передаётся как `&Vec<i32>` (в
  профессиональном коде чаще `&[i32]`; срезы - в `rust-fundamentals`).
* **`match`** (урок 13) - выражение, заменяющее `switch`: точные значения, диапазоны `1..=5`, `|`, `_`, охранное условие
  `if` в ветке. Исчерпываемость показывается ошибкой компилятора `E0004` («non-exhaustive patterns»). `if let` - урок 18.
* **`Option` и `Result` - ровно столько, сколько нужно** (урок 18): `Some`/`None`, `Ok`/`Err`, `match`, `if let`,
  `unwrap_or`, `is_some`, `parse::<i32>()` с обработкой ошибки, `vec.get(i)`, `map.get(key)`. Оператор `?`, `expect`,
  собственные типы ошибок - урок 22 (`?` объясняется честно, как «вернуть ошибку наружу сразу»), `Box<dyn Error>`, `thiserror` -
  `rust-fundamentals` (`error-handling`).
* **Коллекции без замыканий** (уроки 19-20): `Vec<i32>`, `vec![]`, `push`, `[i]`, `len`, `for x in &v`, `iter().sum::<i32>()`,
  `sort`, `contains`; `HashMap<String, i32>`, `insert`, `get`, `contains_key`, `entry(...).or_insert(0)`, обход пар (порядок
  у `HashMap` не определён - вывод всегда сортируется через `Vec` пар или `BTreeMap`, чтобы тесты были детерминированы).
  Замыкания `|x| ...`, `map`, `filter`, `collect`, итераторы-адаптеры - `rust-fundamentals` (`collections-and-iterators`).
* **Структуры (урок 23) - мост:** `struct` с полями, `impl` с методом `&self` и ассоциированной функцией `new`,
  `#[derive(Debug)]` и `{:?}`. Перечисления с данными, трейты, обобщения - `rust-fundamentals`.
* **Что отложено:** времена жизни, трейты, обобщения (кроме `Vec<T>`/`HashMap<K, V>` как готовых типов), замыкания, `?` в
  `main`, `Box`/`Rc`/`RefCell`, модули, `unsafe`, потоки, `async`, Cargo. Во from-zero они встречаются только в разделах
  «Для тех, кто уже программирует» и «Углубление».

## rust-from-zero (уровень 0)

Id `rust.from-zero.<slug>`; каталог `NN-<slug>`; файл решения - `main.rs`. Все упражнения - `io`. Понятия - `rust.<понятие>`.

| # | slug | Тема | introduces | Проверка |
|---|---|---|---|---|
| **Блок 0. Знакомство** |||||
| 01 | first-program | что такое программа и компилятор; `fn main()`; `println!`, строковый литерал | rust.program, rust.compiler, rust.main-function, rust.println, rust.macro, rust.string-literal | io, A |
| 02 | compiler-errors | как читать сообщение `rustc`: код `E....`, адрес `-->`, метки `^^^`, `help:`; каскад; предупреждения | rust.compile-error, rust.error-location | io, B (исправить) |
| 03 | output | `print!` и `println!`, `\n`, `\"`, `{}` с литералами, `{{ }}`, комментарии | rust.print, rust.escape-sequence, rust.placeholder, rust.comment | io, B |
| **Блок 1. Значения** |||||
| 04 | variables | `let`, `let mut`, присваивание, вывод типа, `i32`, `{name}` в строке формата | rust.variable, rust.let, rust.mut, rust.assignment, rust.type-inference, rust.i32, rust.inline-format | io, B |
| 05 | basic-types | `i64`, `f64`, `bool`, `char`, явный тип `: Тип`, другие размеры целых одним абзацем | rust.type, rust.i64, rust.f64, rust.bool, rust.char, rust.explicit-type | io, B |
| 06 | arithmetic | `+ - * / %`, целочисленное деление, приоритет, `+=`, нет `++`, нет смешения типов | rust.arithmetic, rust.integer-division, rust.remainder, rust.compound-assignment | io, B |
| 07 | strings | `&str` и `String`, `String::from`, `push_str`, `len()`, `chars().count()`, `to_uppercase()`, вызов метода | rust.str, rust.string, rust.method-call, rust.push-str, rust.string-length, rust.string-case, rust.usize | io, B |
| 08 | reading-input | `read_to_string`, stdin, `lines()`, `next().unwrap()`, `trim()` | rust.stdin, rust.read-to-string, rust.lines, rust.unwrap, rust.trim | io, C |
| 09 | type-conversion | `parse::<T>()`, `as`, `to_string()`, затенение | rust.parse, rust.as-cast, rust.to-string, rust.shadowing | io, C |
| 10 | formatted-output | `{:.2}`, ширина и выравнивание `{:>8}` `{:<8}`, `{:05}`, `format!`, независимость от локали | rust.format-spec, rust.precision, rust.width-alignment, rust.format-macro | io, C |
| 11 | comparison-and-logic | `== != < <= > >=`, `&& \|\| !`, `bool`-выражения, сравнение `String` с `&str`; типы должны совпадать | rust.comparison, rust.logical-operators | io, C |
| **Блок 2. Управление потоком** |||||
| 12 | if-else | `if`, `else`, `else if`, `if` как выражение, условие только `bool` | rust.if, rust.else, rust.if-expression | io, C |
| 13 | match | `match` по числу, диапазону, строке, `char`; `_`, `\|`, охрана `if`; исчерпываемость | rust.match, rust.match-arm, rust.wildcard, rust.range-pattern | io, C |
| 14 | while-loop | `while`, `loop` + `break`, `continue`, счётчик, накопление | rust.while, rust.loop, rust.break, rust.continue | io, C |
| 15 | for-loop | `for i in 1..=n`, `0..n`, `.rev()`, `.step_by()`, `for line in text.lines()`, вложенные циклы | rust.for, rust.range, rust.nested-loops, rust.read-until-end | io, C |
| **Блок 3. Функции, владение и коллекции** |||||
| 16 | functions | `fn`, параметры с типами, `-> Тип`, последнее выражение без `;`, `return`, `()`, область видимости | rust.function, rust.parameter, rust.return, rust.unit, rust.scope | io (готовый `main`), C |
| 17 | ownership-basics | перемещение `String` в функцию (`E0382`), `&`, `&mut`, `.clone()`, `*` для записи через `&mut` | rust.move, rust.reference, rust.mut-reference, rust.clone | io (готовый `main`), C |
| 18 | option-and-result | `Option`, `Result`, `Some/None`, `Ok/Err`, `match`, `if let`, `unwrap_or`, разбор `parse` и `get` | rust.option, rust.result, rust.if-let, rust.unwrap-or | io, C |
| 19 | vectors | `Vec<i32>`, `vec![]`, `push`, `[i]`, `len`, `for x in &v`, `{:?}`, `sort`, `sum`, `contains`, `&Vec<i32>` | rust.vec, rust.vec-index, rust.vec-iteration, rust.debug-format | io, C |
| 20 | hash-maps | `HashMap<String, i32>`, `insert`, `get`, `entry().or_insert`, обход, сортировка ключей | rust.hash-map, rust.key-value, rust.map-iteration | io, C |
| 21 | string-methods | `split`, `split_whitespace`, `split_once`, `trim`, `contains`, `replace`, `starts_with`, `chars()`, `join` | rust.string-methods, rust.split, rust.chars | io, C |
| **Блок 4. Надёжность и мост к first-steps** |||||
| 22 | errors | `panic!`, `expect`, функции с `Result<T, String>`, `?`, проверка входных данных | rust.panic, rust.error-propagation, rust.question-mark | io, C |
| 23 | first-struct | первая `struct` и `impl`: поля, `new`, метод `&self`, `#[derive(Debug)]`, `{:?}` | rust.struct, rust.field, rust.impl, rust.method-definition, rust.derive-debug | io, C |
| 24 | mini-project | мини-проект: ведомость оценок из строк `имя;балл` (плохие строки считаются и пропускаются) | - | io, D |

Правила для последующих уроков (12-24):

* Уроки 12-15 используют числа, `char` и `String` в условиях. С урока 16 заготовка содержит готовый `main`, который читает
  ввод и вызывает функцию ученика; тесты варьируют входные данные.
* Урок 17 - единственное место, где объясняется «почему компилятор не даёт использовать `String` после передачи»; он
  ссылается на `rust-first-steps` (урок 02) за общей картиной (три правила, `Copy`, `&mut` и ограничения одновременных ссылок).
* Урок 18 вводит `Option`/`Result` через уже встречавшиеся `parse()` и `lines.next()`: теперь `unwrap` заменяется на
  `match`/`if let`/`unwrap_or`, и нечисловой ввод перестаёт быть паникой.
* Уроки 19-21 используют `&Vec<i32>` и `&str` в параметрах функций; обход ключей `HashMap` всегда отсортирован.
* Урок 22 использует `Result<i32, String>` из собственных функций; оператор `?` - только в функциях, возвращающих `Result`
  (`main` с `Result` не преподаётся).
* Урок 23 - мост: `struct Student { name: String, score: i32 }` в том же `main.rs`. Следующий курс начинается с `rustc` и `i64`.
* Урок 24 использует `split_once(';')`, `parse::<i32>()` с `match`, `HashMap`/`Vec`, `struct`.

## rust-for-programmers (уровень 1)

Id `rust.programmers.<slug>`; упражнение - io с готовым `main`; квиз на ловушки Rust: неизменяемость по умолчанию,
отсутствие неявных преобразований чисел, `len()` в байтах и запрет `s[i]`, `String` и `&str`, перемещение при передаче в
функцию, `unwrap` как паника, `HashMap` без порядка, `i32` переполняется в релизной сборке молча.

| # | slug | assessmentFor (`rust.from-zero.*`) |
|---|---|---|
| 01 | syntax-types-and-control-flow | 01-15 |
| 02 | functions-ownership-and-collections | 16, 17, 19, 20, 21 |
| 03 | option-result-errors-and-structs | 18, 22, 23 |

Урок 24 (мини-проект) в зачёт не входит.

## rust-skill-check

Id `rust.check.<slug>`; урок-проверка = упражнение (io) + квиз на чтение кода (порог 0.75).

| Компетенция | slug | assessmentFor |
|---|---|---|
| Основы: значения, ввод, условия, циклы | basics | from-zero 01-15 |
| Функции, векторы, словари, строки | collections | from-zero 16, 19, 20, 21 |
| Ошибки и ввод | errors-input | from-zero 08, 09, 18, 22 |
| Владение и заимствование | ownership | from-zero 17; rust.first-steps.ownership-moves-and-borrowing; rust.fundamentals.ownership-and-moves-in-depth; rust.fundamentals.borrowing-and-lifetimes |
| Структуры, перечисления, `match` | enums-and-matching | from-zero 23; rust.fundamentals.structs-enums-pattern-matching |
| Обработка ошибок | error-handling | rust.fundamentals.error-handling |
| Трейты и обобщения | traits-and-generics | rust.fundamentals.traits-and-generics |
| Коллекции и итераторы | iterators | rust.fundamentals.collections-and-iterators |

## Связи

* `rust-first-steps`: `requires: [rust-from-zero]`; урок 01 (`rust.first-steps.how-rust-runs`) зависит от
  `rust.from-zero.first-struct` (урок 23). Урок 24 (мини-проект) - необязательное завершение; его `next` - урок
  `rust.first-steps.how-rust-runs`. Цель урока 01 first-steps («написать программу, читающую числа из stdin») к этому
  моменту достигнута во from-zero, поэтому формулировки смещаются к `rustc` и отсутствию рантайма, `i64`, `as` и
  переполнению. Его упражнение использует `split_whitespace`, `take`, `map(|t| ...)`, `collect::<Vec<i64>>()`: замыкание
  и `collect` во from-zero не вводятся, поэтому в фазе B подсказки этого упражнения переписываются на цикл `for` и `Vec` с
  `push` (или урок 01 first-steps явно вводит замыкание как новое понятие). Урок 02 first-steps не повторяет `let mut`,
  `String`/`&str` и `&`/`&mut` в параметрах, а берёт их как известные: он даёт три правила владения, `Copy` и `clone`,
  причины ошибок `E0382`, `E0499`, `E0502`, `E0106`.
* `rust-fundamentals`: `requires: [rust-first-steps]` (как сейчас).
* `rust-for-programmers`: последний урок ведёт в `rust.first-steps.how-rust-runs`.
