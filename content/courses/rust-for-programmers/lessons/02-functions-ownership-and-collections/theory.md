<!-- section: simple -->
## Зачем этот урок

Функции, строки, списки и словари в Rust выглядят привычно, но в них скрыто главное отличие языка: у каждого значения **ровно один владелец**. Когда вы передаёте `String` или `Vec` в функцию, значение **перемещается**: прежняя переменная больше не может его использовать. Чтобы функция только прочитала данные, ей передают ссылку `&`; чтобы изменила - изменяемую ссылку `&mut`, причём одновременно допустимо либо много читателей, либо один писатель. Эти правила проверяет компилятор, и благодаря им в Rust нет сборщика мусора, висячих указателей и гонок данных. Успешное прохождение этого урока (упражнение и квиз) засчитывает уроки 16, 17, 19, 20 и 21 курса «Rust с нуля».

## Главная мысль

Спрашивайте себя, кто владеет значением и что функции с ним делать: забрать (`String`, `Vec<T>`), прочитать (`&str`, `&Vec<T>`) или изменить (`&mut Vec<T>`). Для чтения принимают ссылку; забирают значение, только когда оно действительно уходит дальше (в структуру, в другой поток, в результат). Подробная теория владения - в курсе «Rust: первые шаги», урок 2; здесь важно усвоить практическую часть и типичные сообщения компилятора.

<!-- section: examples -->
## Пример 1. Перемещение и заимствование

```rust run
fn consume(text: String) -> usize {
    text.len()
}

fn peek(text: &str) -> usize {
    text.chars().count()
}

fn shout(text: &mut String) {
    text.push('!');
}

fn bump(counter: &mut i32) {
    *counter += 1;
}

fn main() {
    let name = String::from("Анна");
    println!("{}", peek(&name));

    let mut greeting = name.clone();
    shout(&mut greeting);
    println!("{greeting} {name}");

    let a = String::from("x");
    let b = a;
    println!("{b}");

    let n = 5;
    let m = n;
    println!("{n} {m}");

    let mut counter = 10;
    bump(&mut counter);
    bump(&mut counter);
    println!("{counter}");

    println!("{}", consume(name));
}
```

```output
4
Анна! Анна
x
5 5
12
8
```

Разбор. Функция `consume` принимает `String` по значению: после `consume(name)` переменная `name` недоступна (это последняя строка `main`, поэтому ошибки нет). Функция `peek` принимает `&str`: заимствованный просмотр текста, подходит и для литерала `"abc"`, и для `&name` (ссылка на `String` превращается в `&str` автоматически). Для изменения нужны два действия: `&mut` и в параметре, и в вызове (`&mut greeting`), а сама переменная объявляется с `let mut`. Для числа через `&mut i32` значение записывают звёздочкой: `*counter += 1`. `let b = a` для `String` - перемещение, а для чисел `let m = n` - **копия**, потому что `i32`, `bool`, `char` и `f64` относятся к типам с `Copy`: после присваивания пользоваться можно обеими переменными. `clone()` - явная глубокая копия, её видно в коде, поэтому дорогие копии не появляются незаметно.

**Попробуйте.** Поставьте `println!("{name}");` после строки `println!("{}", consume(name));` и прочитайте сообщение компилятора: оно называет место перемещения.

## Пример 2. Вектор

```rust run
fn last_index(values: &Vec<i32>) -> usize {
    values.len() - 1
}

fn main() {
    let mut v = vec![30, -10, 20];
    v.push(5);
    println!("{} {} {:?}", v.len(), v[0], v);
    println!("{:?} {:?}", v.get(1), v.get(10));

    for x in &mut v {
        *x *= 2;
    }
    v.retain(|x| *x > 0);
    println!("{:?}", v);

    v.sort();
    v.dedup();
    println!("{:?} {}", v, v.contains(&40));

    let total: i32 = v.iter().sum();
    let doubled: Vec<i32> = v.iter().map(|x| x + 1).collect();
    println!("{total} {:?}", doubled);

    let zeros = vec![0; 3];
    let grid = vec![vec![7; 2]; 2];
    println!("{:?} {:?}", zeros, grid);

    let empty: Vec<i32> = Vec::new();
    println!("{}", last_index(&v));
    println!("{}", last_index(&empty));
    println!("{:?}", empty.len().checked_sub(1));
}
```

```output
4 30 [30, -10, 20, 5]
Some(-10) None
[60, 40, 10]
[10, 40, 60] true
110 [11, 41, 61]
[0, 0, 0] [[7, 7], [7, 7]]
2
18446744073709551615
None
```

Разбор. Макрос `vec![...]` создаёт вектор; `vec![0; 3]` - три нуля. Вектор владеет своими элементами, и изменять его можно только если он объявлен `mut`. Индексация `v[i]` проверяет границы и при выходе за них останавливает программу паникой, а `v.get(i)` возвращает `Option`: `Some(&значение)` или `None`. В `{:?}` вектор печатается в квадратных скобках. Цикл `for x in &mut v` даёт ссылку на каждый элемент, поэтому изменять нужно через `*x`. Выражение `|x| *x > 0` - **замыкание**, безымянная функция с параметром `x`: `retain` оставляет элементы, для которых оно истинно. Цепочка `iter().map(...).collect()` - привычные `map` и `toList`: `iter()` даёт ссылки на элементы, `map` преобразует, `collect` собирает результат в вектор с типом, указанным слева. Особая ловушка последних строк: длина и индексы имеют тип `usize`, он не бывает отрицательным. Для пустого вектора `len() - 1` в отладочной сборке остановило бы программу, а в сборке с `-O` результат заворачивается в `18446744073709551615`. Безопасный вариант - `checked_sub`.

## Пример 3. Словарь и строки

```rust run
use std::collections::HashMap;

fn main() {
    let mut stock: HashMap<String, i32> = HashMap::new();
    stock.insert(String::from("apple"), 3);
    *stock.entry(String::from("pear")).or_insert(0) += 2;
    *stock.entry(String::from("apple")).or_insert(0) += 1;
    println!("{:?} {:?}", stock.get("apple"), stock.get("kiwi"));
    println!("{} {}", stock.contains_key("pear"), stock.len());

    let mut names: Vec<&String> = stock.keys().collect();
    names.sort();
    for name in names {
        println!("{name}={}", stock[name]);
    }

    let line = "  name=Anna; score=90 ";
    let parts: Vec<&str> = line.trim().split(';').collect();
    println!("{:?}", parts);
    if let Some((key, value)) = parts[0].split_once('=') {
        println!("{key} -> {value}");
    }
    println!("{:?} {:?}", line.find('='), line.find('#'));
    let words: Vec<&str> = "a b  c".split_whitespace().collect();
    println!("{} {}", words.len(), words.join("-"));
    println!("{}", "banana".replace("an", "AN"));
    let reversed: String = "привет".chars().rev().collect();
    println!("{reversed}");
}
```

```output
Some(4) None
true 2
apple=4
pear=2
["name=Anna", " score=90"]
name -> Anna
Some(6) None
3 a-b-c
bANANa
тевирп
```

Разбор. `HashMap::new()` создаёт пустой словарь; тип записывается слева. Метод `get` возвращает `Option<&V>`, поэтому отсутствие ключа - это `None`, а не `null` и не исключение. Запись `stock["kiwi"]` для отсутствующего ключа вызвала бы панику. Идиома подсчёта - `entry(ключ).or_insert(0)`: возвращает изменяемую ссылку на значение (создав его при необходимости), которую увеличивают через `*`. **Порядок обхода `HashMap` не определён** и может отличаться от запуска к запуску, поэтому ключи сначала собирают в вектор и сортируют; если нужен упорядоченный словарь, берут `BTreeMap`. Строковые методы возвращают срезы исходной строки: `split` - итератор, который собирают в `Vec<&str>`, `split_once` - `Option` с парой частей, `find` - `Option<usize>` (позиция в байтах или `None`, а не `-1`). Метод `join` склеивает срезы через разделитель, `replace` возвращает новую строку.

<!-- section: precise -->
## Точные правила

* Параметр типа `String` или `Vec<T>` забирает значение: переменная в вызывающем коде после вызова недоступна. Параметр `&T` только читает, `&mut T` читает и изменяет. В вызове нужно писать `&` и `&mut` явно.
* В любой момент существует либо любое число ссылок `&T`, либо одна ссылка `&mut T`; ссылка не может пережить владельца. Это проверяется при компиляции.
* Типы с `Copy` (все числа, `bool`, `char`, ссылки `&T`, кортежи из них) копируются при присваивании и передаче. `String`, `Vec`, `HashMap` не `Copy`: они перемещаются, а `clone()` создаёт явную копию.
* Для чтения принято принимать `&str`, а не `&String`, и срез `&[T]`, а не `&Vec<T>`. Компилятор подставит `&String` на место `&str`, а `&Vec<T>` на место `&[T]` автоматически.
* `v[i]` проверяет границы и паникует; `v.get(i)` возвращает `Option<&T>`; `v.first()`, `v.last()` - тоже `Option`. Индекс и длина - `usize`.
* `usize` не бывает отрицательным: вычитание ниже нуля паникует в отладочной сборке и заворачивается в релизной (`-O`). Используйте `checked_sub` или сравнивайте до вычитания.
* `for x in v` забирает вектор, `for x in &v` читает, `for x in &mut v` даёт `&mut` к элементам.
* `HashMap<K, V>`: `insert`, `get`, `contains_key`, `remove`, `entry(k).or_insert(v)`; `map[k]` паникует, если ключа нет. Порядок обхода не определён; для порядка по ключу используют `BTreeMap`.
* `String` изменяется (`push`, `push_str`, `insert`), `&str` - нет. `s1 + &s2` перемещает `s1`; для сборки текста удобнее `format!`.
* Методы `split`, `split_whitespace`, `trim`, `lines` возвращают итераторы или срезы, которые ссылаются на исходную строку. Они не копируют текст, пока вы не вызовете `to_string()` или `String::from`.

<!-- section: mistakes -->
## Типичные ошибки

Сообщения ниже получены настоящим `rustc` 1.99; пути к стандартной библиотеке заменены многоточием.

**1. Использование значения после перемещения.**

```rust
fn count_chars(text: String) -> usize {
    text.chars().count()
}

fn main() {
    let title = String::from("Война и мир");
    let n = count_chars(title);
    println!("{title}: {n}");
}
```

```text
error[E0382]: borrow of moved value: `title`
 --> main.rs:8:16
  |
6 |     let title = String::from("Война и мир");
  |         ----- move occurs because `title` has type `String`, which does not implement the `Copy` trait
7 |     let n = count_chars(title);
  |                         ----- value moved here
8 |     println!("{title}: {n}");
  |                ^^^^^ value borrowed here after move
  |
note: consider changing this parameter type in function `count_chars` to borrow instead if owning the value isn't necessary
 --> main.rs:1:22
  |
1 | fn count_chars(text: String) -> usize {
  |    -----------       ^^^^^^ this parameter takes ownership of the value
  |    |
  |    in this function
help: consider cloning the value if the performance cost is acceptable
  |
7 |     let n = count_chars(title.clone());
  |                              ++++++++
```

Исправления в порядке предпочтения: принять `&str` в функции и вызвать `count_chars(&title)`; передать `title.clone()`, если копия действительно нужна.

**2. Забыли `&` в вызове.**

```rust
fn first_word(text: &str) -> &str {
    text.split_whitespace().next().unwrap_or("")
}

fn main() {
    let line = String::from("hello world");
    println!("{}", first_word(line));
}
```

```text
error[E0308]: mismatched types
 --> main.rs:7:31
  |
7 |     println!("{}", first_word(line));
  |                    ---------- ^^^^ expected `&str`, found `String`
  |                    |
  |                    arguments to this function are incorrect
  |
note: function defined here
 --> main.rs:1:4
  |
1 | fn first_word(text: &str) -> &str {
  |    ^^^^^^^^^^ ----------
help: consider borrowing here
  |
7 |     println!("{}", first_word(&line));
  |                               +
```

Функция ждёт ссылку, а получила саму `String`. Компилятор подсказывает поставить `&` перед аргументом.

**3. Изменение через неизменяемую ссылку или переменную.**

```rust
fn add_item(items: &Vec<i32>) {
    items.push(1);
}

fn main() {
    let mut list = Vec::new();
    add_item(&list);
    list.push(2);
    println!("{:?}", list);
}
```

```text
error[E0596]: cannot borrow `*items` as mutable, as it is behind a `&` reference
 --> main.rs:2:5
  |
2 |     items.push(1);
  |     ^^^^^ `items` is a `&` reference, so it cannot be borrowed as mutable
  |
help: consider changing this to be a mutable reference
  |
1 | fn add_item(items: &mut Vec<i32>) {
  |                     +++
```

Для изменения параметр объявляют `&mut Vec<i32>`, а вызов пишут `add_item(&mut list)`.

**4. Две изменяемые ссылки сразу.**

```rust
fn main() {
    let mut text = String::from("a");
    let first = &mut text;
    let second = &mut text;
    first.push('b');
    second.push('c');
    println!("{text}");
}
```

```text
error[E0499]: cannot borrow `text` as mutable more than once at a time
 --> main.rs:4:18
  |
3 |     let first = &mut text;
  |                 --------- first mutable borrow occurs here
4 |     let second = &mut text;
  |                  ^^^^^^^^^ second mutable borrow occurs here
5 |     first.push('b');
  |     ----- first borrow later used here
```

Один писатель в каждый момент - правило, которое исключает гонки данных и недействительные итераторы. Закончите работу с первой ссылкой, прежде чем брать вторую.

**5. Изменение вектора во время чтения его элемента.**

```rust
fn main() {
    let mut v = vec![1, 2, 3];
    let first = &v[0];
    v.push(4);
    println!("{first}");
}
```

```text
error[E0502]: cannot borrow `v` as mutable because it is also borrowed as immutable
 --> main.rs:4:5
  |
3 |     let first = &v[0];
  |                  - immutable borrow occurs here
4 |     v.push(4);
  |     ^^^^^^^^^ mutable borrow occurs here
5 |     println!("{first}");
  |                ----- immutable borrow later used here
```

`push` может перенести вектор в новую память, и ссылка `first` указала бы в пустоту. В C++ это неопределённое поведение, а в Rust - ошибка компиляции.

**6. Индекс вне границ.**

```rust
use std::collections::HashMap;

fn main() {
    let v = vec![1, 2, 3];
    let i: usize = "5".parse().unwrap();
    let mut m: HashMap<&str, i32> = HashMap::new();
    m.insert("a", 1);
    println!("{}", m["a"]);
    println!("{}", v[i]);
}
```

```text
thread 'main' panicked at main.rs:9:21:
index out of bounds: the len is 3 but the index is 5
```

Первое обращение (`m["a"]`) удалось, второе остановило программу. Так же завершается обращение `m["b"]` к отсутствующему ключу словаря. Обе проверки идут во время работы. Для данных, которым нельзя доверять, берите `get` и обрабатывайте `None`.

**7. Возврат ссылки на данные, которых больше нет.** Функция `fn make() -> &str { let s = String::from("x"); &s }` не скомпилируется: сначала `E0106` (нет времени жизни у возвращаемой ссылки), а если время жизни дописать, то `E0515` (ссылка на локальную переменную). Возвращайте `String` по значению.

**8. Размер в байтах.** `"привет".len()` равно `12`. Если нужно число букв, считайте `chars().count()`, а `&text[0..1]` на кириллице остановит программу паникой.

<!-- section: summary -->
## Итог

Вы знаете практическую сторону владения: `String` и `Vec` перемещаются, для чтения передают `&`, для изменения `&mut`, одновременно возможен один писатель или много читателей; `get` вместо индекса возвращает `Option`, `usize` не бывает отрицательным, а порядок обхода `HashMap` не определён. Следующий урок - `Option`, `Result`, ошибки и структуры: [Option, Result, ошибки и структуры](lesson:rust.programmers.option-result-errors-and-structs). Для подробностей: [Функции](lesson:rust.from-zero.functions), [Владение и заимствование](lesson:rust.from-zero.ownership-basics), [Векторы](lesson:rust.from-zero.vectors), [Словари](lesson:rust.from-zero.hash-maps), [Методы строк](lesson:rust.from-zero.string-methods).

<!-- section: programmer -->
## Главное для тех, кто уже программирует

* Присваивание и передача `String`, `Vec`, `HashMap` - перемещение, а не копирование ссылки (Java, Python, JavaScript) и не глубокое копирование (C++). В C++ это ближе всего к `std::move`, только применяется автоматически и проверяется компилятором. Копия - явный `clone()`.
* Ссылки `&T` и `&mut T` похожи на указатели и ссылки C++, но не бывают нулевыми, не бывают висячими и подчиняются правилу «много читателей или один писатель». Нулевого указателя нет: отсутствие выражает `Option<T>`.
* Нет исключений: выход за границы и `unwrap` на пустом значении - паника, то есть аварийное завершение, а восстановимые ошибки возвращаются как `Result` (следующий урок).
* `Vec<T>` - аналог `ArrayList`, `std::vector`, списка Python и слайса Go, но с проверкой границ. `HashMap` не упорядочен (как в Java, Go и C++ `unordered_map`); в Python 3.7+ и JavaScript `Map` порядок вставки сохраняется, в Rust - нет. Отсортированный аналог - `BTreeMap` (как `TreeMap`).
* Строка - UTF-8, как в Go; `len()` в байтах. `&str` соответствует `string_view` из C++ или срезу строки в Go, `String` - `std::string` или `StringBuilder`.
* Итераторы ленивы и компилируются в такой же быстрый цикл, как ручной; `collect` нужен, чтобы получить коллекцию. Замыкания `|x| x + 1` - аналог лямбд.
* Конструкции `for x in v` по значению и `for x in &v` по ссылке - первый раз встречаются у новичков как неожиданное «вектор исчез»: после `for x in v` вектор перемещён.
