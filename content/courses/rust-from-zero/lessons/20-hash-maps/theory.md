<!-- section: simple -->
## Зачем нужен словарь

Вектор подходит, когда данные идут по порядку и вы обращаетесь к ним по номеру. Но часто нужно другое: «сколько осталось яблок», «сколько раз встретилось слово», «какой балл у Анны». Номер тут бесполезен - есть **имя**. Искать по имени в векторе пришлось бы перебором всех элементов.

**Словарь** (`HashMap`) хранит **пары «ключ - значение»**. **Ключ** - то, по чему ищем (например, название товара), **значение** - то, что хотим получить (количество). Каждый ключ встречается один раз: повторная запись по тому же ключу заменяет значение. Поиск по ключу быстрый и не зависит от размера словаря.

## Одна новая идея: `HashMap<String, i32>`

Запись `HashMap<String, i32>` читается «словарь из `String` в `i32`»: ключи - строки, значения - целые числа. Тип нужно знать компилятору, поэтому его пишут при создании: `let mut m: HashMap<String, i32> = HashMap::new();`.

Одна строка шаблона неизбежна. `HashMap` не входит в набор типов, доступных без объявления (в отличие от `Vec` и `String`), поэтому в начале файла пишут `use std::collections::HashMap;`. Это значит «возьми тип `HashMap` из раздела `collections` стандартной библиотеки». Подробно о модулях рассказывается в `rust-fundamentals`; пока считайте эту строку обязательным началом программы, где нужен словарь.

<!-- section: examples -->
## Пример 1. `insert`, `get`, `contains_key`

```rust run
use std::collections::HashMap;

fn main() {
    let mut stock: HashMap<String, i32> = HashMap::new();
    stock.insert(String::from("яблоки"), 12);
    stock.insert(String::from("груши"), 5);
    stock.insert(String::from("яблоки"), 20);

    println!("видов: {}", stock.len());
    println!("{:?}", stock.get("яблоки"));
    println!("{:?}", stock.get("сливы"));
    println!("{}", stock.contains_key("груши"));

    if let Some(n) = stock.get("груши") {
        println!("груш: {n}");
    }
}
```
```output
видов: 2
Some(20)
None
true
груш: 5
```

* `insert(ключ, значение)` добавляет пару. Ключ - именно `String`, поэтому литерал оборачивается в `String::from(...)`. Вторая запись для `"яблоки"` **заменила** значение 12 на 20: размер словаря остался 2.
* `get("яблоки")` ищет по ключу. Здесь можно передать `&str`, `String::from` не нужен. Результат - `Option`: `Some(20)`, если ключ есть, и `None`, если нет. Это уже знакомый разбор из урока про `Option`.
* `contains_key("груши")` отвечает `true` / `false`.
* `if let Some(n) = stock.get("груши")` - привычная проверка «если значение найдено, возьми его».

**Попробуйте.** Добавьте `stock.insert(String::from("сливы"), 8);` и выведите `stock.len()`. Затем замените значение для `"груши"`.

## Пример 2. Считаем повторения: `entry` и `or_insert`

```rust run
use std::collections::HashMap;

fn main() {
    let words = vec!["кот", "пёс", "кот", "кот", "пёс", "рыба"];

    let mut counts: HashMap<String, i32> = HashMap::new();
    for w in words {
        *counts.entry(String::from(w)).or_insert(0) += 1;
    }

    let mut names: Vec<&String> = Vec::new();
    for name in counts.keys() {
        names.push(name);
    }
    names.sort();

    for name in names {
        println!("{name}: {}", counts[name]);
    }
}
```
```output
кот: 3
пёс: 2
рыба: 1
```

Главный приём работы со словарём: «если ключа нет, начни с нуля, потом прибавь один».

* `counts.entry(String::from(w))` - «запись для этого ключа». `.or_insert(0)` - «если её ещё нет, вставь 0» и вернуть изменяемую ссылку на значение. Результат - `&mut i32`.
* `*... += 1` - запись через ссылку, как в уроке про владение: звёздочка означает «само число, на которое указывает ссылка».
* Цикл `for name in counts.keys()` перебирает ключи, а `names.sort()` упорядочивает их. Так вывод получает предсказуемый порядок: сначала по алфавиту ключи, потом `counts[name]` берёт значение по ключу.
* `counts[name]` - ещё один способ чтения. Он допустим здесь, потому что ключ точно есть (мы взяли его из самого словаря).

## Пример 3. Словарь в функциях

```rust run
use std::collections::HashMap;

fn add_sale(totals: &mut HashMap<String, i32>, item: &str, qty: i32) {
    *totals.entry(String::from(item)).or_insert(0) += qty;
}

fn sum_all(totals: &HashMap<String, i32>) -> i32 {
    let mut sum = 0;
    for (_item, qty) in totals {
        sum += qty;
    }
    sum
}

fn main() {
    let mut totals: HashMap<String, i32> = HashMap::new();
    add_sale(&mut totals, "чай", 3);
    add_sale(&mut totals, "кофе", 2);
    add_sale(&mut totals, "чай", 4);

    println!("чай: {}", totals["чай"]);
    println!("всего: {}", sum_all(&totals));
}
```
```output
чай: 7
всего: 9
```

* `totals: &mut HashMap<String, i32>` - функция меняет словарь; вызывают с `&mut totals`. `&HashMap<String, i32>` - только читать.
* `for (_item, qty) in totals` перебирает **пары** сразу: в круглых скобках два имени, одно для ключа и одно для значения. Нижнее подчёркивание в начале имени (`_item`) говорит компилятору, что переменная не используется.
* Сумма `sum += qty` работает без звёздочки: `qty` - ссылка на число.

## Пример 4. Подсчёт голосов из ввода

Программа читает ввод, поэтому приведены ввод и вывод.

```rust
use std::collections::HashMap;

fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut votes: HashMap<String, i32> = HashMap::new();
    for line in text.lines() {
        let name = line.trim();
        if name != "" {
            *votes.entry(String::from(name)).or_insert(0) += 1;
        }
    }

    let mut names: Vec<&String> = Vec::new();
    for name in votes.keys() {
        names.push(name);
    }
    names.sort();
    for name in names {
        println!("{name}: {}", votes[name]);
    }
}
```

Ввод:

```text
Анна
Борис

Анна
 Борис 
Анна
Вера
```

Вывод:

```text
Анна: 3
Борис: 2
Вера: 1
```

Пустая строка пропущена проверкой `name != ""`, пробелы убраны `trim`, и «Борис» с пробелами и без них засчитан как один ключ. Для обхода сначала собираются ключи, сортируются, и только потом печатаются.

<!-- section: precise -->
## Точные правила

* `HashMap<K, V>` хранит пары: ключ типа `K` и значение типа `V`. Ключи уникальны; `insert` по существующему ключу заменяет значение.
* `use std::collections::HashMap;` нужен в начале файла. Без него имя `HashMap` неизвестно компилятору.
* `HashMap::new()` создаёт пустой словарь; типы ключа и значения указываются в аннотации переменной или определяются по первым `insert`.
* `m.insert(k, v)`, `m.entry(k).or_insert(v)` и присваивание через `*` требуют `mut`; `get`, `contains_key`, `len`, `keys` - нет.
* `m.get(&k)` / `m.get("ключ")` возвращают `Option` со ссылкой на значение. `m[ключ]` читает значение напрямую, но если ключа нет, программа останавливается с паникой.
* `m.contains_key(ключ)` - `bool`; `m.len()` - число пар; `m.remove(ключ)` удаляет пару; `m.keys()` и `m.values()` перебирают ключи и значения.
* `for (k, v) in &m` перебирает пары, `v` и `k` - ссылки. **Порядок обхода не определён** и может отличаться между запусками. Для стабильного вывода сортируйте ключи.
* Ключи типа `String` нужно создавать как `String` (`String::from(...)`); для поиска можно использовать `&str`.
* Значение, полученное через `entry(...).or_insert(...)`, - `&mut V`; менять его нужно через `*`.

<!-- section: mistakes -->
## Типичные ошибки

**1. Забыли `use`.**

```rust
fn main() {
    let mut m: HashMap<String, i32> = HashMap::new();
    m.insert(String::from("a"), 1);
    println!("{}", m.len());
}
```

```text
error[E0425]: cannot find type `HashMap` in this scope
 --> main.rs:2:16
  |
2 |     let mut m: HashMap<String, i32> = HashMap::new();
  |                ^^^^^^^ not found in this scope
  |
help: consider importing this struct
  |
1 + use std::collections::HashMap;
  |

error[E0433]: cannot find type `HashMap` in this scope
 --> main.rs:2:39
  |
2 |     let mut m: HashMap<String, i32> = HashMap::new();
  |                                       ^^^^^^^ use of undeclared type `HashMap`
  |
help: consider importing this struct
  |
1 + use std::collections::HashMap;
  |


Some errors have detailed explanations: E0425, E0433.
For more information about an error, try `rustc --explain E0425`.
```

Компилятор сам предлагает нужную строку `use`. Добавьте её в начало файла.

**2. `&str` вместо `String` в ключе.**

```rust
use std::collections::HashMap;

fn main() {
    let mut m: HashMap<String, i32> = HashMap::new();
    m.insert("яблоки", 3);
    println!("{}", m.len());
}
```

```text
error[E0308]: mismatched types
    --> main.rs:5:14
     |
   5 |     m.insert("яблоки", 3);
     |       ------ ^^^^^^^^ expected `String`, found `&str`
     |       |
     |       arguments to this method are incorrect
     |
note: method defined here
    --> ...:1332:12
     |
1332 |     pub fn insert(&mut self, k: K, v: V) -> Option<V> {
     |            ^^^^^^
help: try using a conversion method
     |
   5 |     m.insert("яблоки".to_string(), 3);
     |                      ++++++++++++
```

Ключ словаря объявлен как `String`, а литерал - это `&str`. Оберните его в `String::from("яблоки")`.

**3. Забыли `*` при изменении значения.**

```rust
use std::collections::HashMap;

fn main() {
    let mut counts: HashMap<String, i32> = HashMap::new();
    counts.entry(String::from("a")).or_insert(0) += 1;
    println!("{}", counts.len());
}
```

```text
error[E0368]: binary assignment operation `+=` cannot be applied to type `&mut i32`
 --> main.rs:5:5
  |
5 |     counts.entry(String::from("a")).or_insert(0) += 1;
  |     --------------------------------------------^^^^^
  |     |
  |     cannot use `+=` on type `&mut i32`
  |
help: `+=` can be used on `i32` if you dereference the left-hand side
  |
5 |     *counts.entry(String::from("a")).or_insert(0) += 1;
  |     +
```

`or_insert` возвращает ссылку, а `+=` нужно применить к числу по ссылке. Напишите `*counts.entry(...).or_insert(0) += 1;`.

**4. Печать результата `get` через `{}`.**

```rust
use std::collections::HashMap;

fn main() {
    let mut m: HashMap<String, i32> = HashMap::new();
    m.insert(String::from("a"), 1);
    println!("{}", m.get("a"));
}
```

```text
error[E0277]: `Option<&i32>` doesn't implement `std::fmt::Display`
 --> main.rs:6:20
  |
6 |     println!("{}", m.get("a"));
  |               --   ^^^^^^^^^^ `Option<&i32>` cannot be formatted with the default formatter
  |               |
  |               required by this formatting parameter
  |
  = help: the trait `std::fmt::Display` is not implemented for `Option<&i32>`
  = note: in format strings you may be able to use `{:?}` (or `{:#?}` for pretty-print) instead
```

`get` возвращает `Option`. Разберите его через `match` / `if let` или напечатайте через `{:?}`.

**5. `m[ключ]` для ключа, которого нет.** Программа компилируется, но падает при запуске:

```rust
use std::collections::HashMap;

fn main() {
    let mut m: HashMap<String, i32> = HashMap::new();
    m.insert(String::from("чай"), 3);
    println!("{}", m["чай"]);
    println!("{}", m["кофе"]);
}
```

Ввод:

```text

```

Результат работы:

```text
thread 'main' (2055526) panicked at main.rs:7:21:
no entry found for key
```

Для ключей, которых может не быть, используйте `get` и разбор `Option`.

**6. Расчёт на порядок.** Если перебрать `HashMap` и напечатать пары, порядок может оказаться другим при следующем запуске. Это не ошибка компилятора и не сбой: порядок просто не определён. Для вывода собирайте ключи в `Vec`, сортируйте и берите значения по ключу, как в примерах 2 и 4.

<!-- section: summary -->
## Итоги

* `HashMap<String, i32>` хранит пары «ключ - значение»; подключается строкой `use std::collections::HashMap;`.
* `insert` добавляет или заменяет, `get` возвращает `Option`, `contains_key` проверяет наличие, `len` считает пары.
* `*map.entry(ключ).or_insert(0) += 1` - стандартный способ считать повторения.
* Порядок обхода `HashMap` не определён: для стабильного вывода сортируйте ключи.
* Для ключей, которых может не быть, используйте `get`, а не `m[ключ]`.

Следующий урок: [методы строк](lesson:rust.from-zero.string-methods): как разрезать строку на части, чтобы получить ключи и значения.

<!-- section: programmer -->
## Для тех, кто уже программирует

* `HashMap<K, V>` - аналог `dict` / `Map` / `map`. Ключ должен уметь сравниваться и хешироваться (`String`, числа, `bool`, `char` подходят; `f64` - нет).
* Порядок обхода намеренно случаен: по умолчанию используется хеш с защитой от атак подбором коллизий (SipHash со случайным начальным значением). Для упорядоченного словаря есть `BTreeMap` из того же модуля `std::collections`, он обходит ключи по возрастанию.
* `entry(...).or_insert(...)` - единая точка «прочитать или создать»: компилятор не даёт сделать лишний поиск и сохраняет безопасность владения.
* `m[key]` паникует на отсутствующем ключе (как `KeyError`), `m.get(key)` возвращает `Option` (как `dict.get` с `None`).
* `insert` возвращает `Option` с прежним значением, если ключ уже был.
* Ключ и значение **принадлежат** словарю: `insert(String::from(...), ...)` передаёт владение ключом.
* Многопоточный доступ и более быстрые хеши (`FxHashMap`, `ahash`) - за пределами этого курса.

<!-- section: deep-dive -->
## Как работает поиск по ключу

Это необязательный раздел: для заданий он не нужен.

`HashMap` хранит пары в большом массиве «корзин». Чтобы найти ключ, он вычисляет **хеш** - число, получаемое из содержимого ключа, - и по нему выбирает корзину. Поиск не просматривает все пары, поэтому его стоимость почти не зависит от размера словаря. Если два разных ключа попали в одну область, словарь проверяет их на равенство; поэтому ключ должен быть сравнимым.

Порядок хранения определяется хешем, а стандартная библиотека каждый раз использует случайное начальное значение хеш-функции: так злоумышленник не сможет подобрать ключи, из-за которых словарь замедлится. Побочный эффект - порядок обхода нельзя предсказать и нельзя на него рассчитывать, поэтому все примеры выше сортируют ключи перед выводом.

Когда словарь заполняется, он увеличивает массив корзин и заново раскладывает пары - так же, как вектор перевыделяет память при `push`.
