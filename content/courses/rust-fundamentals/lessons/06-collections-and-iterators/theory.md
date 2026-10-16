<!-- section: simple -->
## Коллекции и ленивые итераторы

Основные коллекции стандартной библиотеки живут в куче и **владеют** своими элементами:

* `Vec<T>` - растущий массив: быстрый доступ по индексу, добавление в конец.
* `HashMap<K, V>` - словарь на хэш-таблице, порядок обхода **не определён**.
* `HashSet<T>` - множество без повторов, тоже без порядка.
* `BTreeMap<K, V>` и `BTreeSet<T>` - те же словарь и множество, но **упорядоченные** по ключу (на B-дереве).

Обрабатывать коллекции принято **итераторами**: цепочкой «источник -> адаптеры -> потребитель». Адаптеры (`map`, `filter`, `take`, `zip`...) **ленивы**: они ничего не делают, пока потребитель (`collect`, `sum`, `for`, `count`) не потянет значения.

```rust run
use std::collections::HashMap;

fn main() {
    let words = ["apple", "bob", "cat", "apple", "bob", "apple"];

    // Подсчёт слов: entry API находит ячейку и меняет её на месте
    let mut counts: HashMap<&str, u32> = HashMap::new();
    for w in words {
        *counts.entry(w).or_insert(0) += 1;
    }

    // HashMap не упорядочен: для стабильного вывода сортируем
    let mut pairs: Vec<(&str, u32)> = counts.into_iter().collect();
    pairs.sort_by(|a, b| b.1.cmp(&a.1).then(a.0.cmp(b.0)));
    println!("{:?}", pairs);

    // Цепочка адаптеров, ленивая до collect
    let lens: Vec<usize> = words.iter().filter(|w| w.len() > 3).map(|w| w.len()).collect();
    println!("{:?}", lens);
}
```
```output
[("apple", 3), ("bob", 2), ("cat", 1)]
[5, 5, 5]
```

Три формы обхода показывают, кому принадлежат элементы: `for x in &v` / `v.iter()` даёт `&T` (читаем), `for x in &mut v` / `v.iter_mut()` даёт `&mut T` (меняем на месте), `for x in v` / `v.into_iter()` даёт `T` и **поглощает** вектор.

<!-- section: precise -->
## Коллекции и итераторы точно

**Vec.** Хранит `ptr`, `len`, `capacity`. `push` амортизированно O(1), может перераспределить буфер (поэтому ссылки на элементы нельзя держать во время `push` - `E0502`). Полезные методы: `with_capacity`, `extend`, `insert`/`remove` (O(n)), `swap_remove` (O(1), порядок нарушается), `retain`, `dedup`, `sort`/`sort_by_key`/`sort_unstable`, `binary_search`, `windows`, `chunks`, `iter().rev()`, `get(i)` (возвращает `Option`).

**HashMap и HashSet.** Требуют `K: Eq + Hash`. Среднее O(1) на вставку и поиск. Порядок обхода зависит от случайного начального значения хэша и меняется между запусками, поэтому для вывода и тестов сортируйте ключи или берите `BTreeMap`. Методы: `insert` (возвращает старое значение), `get`, `get_mut`, `contains_key`, `remove`, `entry`.

**Entry API.** `map.entry(key)` возвращает `Entry` (занято/свободно) и позволяет сделать «найти или создать» одним поиском: `or_insert(v)`, `or_insert_with(f)`, `or_default()`, `and_modify(|v| ...).or_insert(...)`. Результат `or_insert` - `&mut V`, поэтому часто пишут `*map.entry(k).or_insert(0) += 1`.

**BTreeMap и BTreeSet.** Требуют `K: Ord`. Операции O(log n); обход всегда по возрастанию ключа; есть `range(a..b)`, `first_key_value`, `last_key_value`, `pop_first`.

**Итератор** - любой тип с `Iterator::next(&mut self) -> Option<Self::Item>`. Три вида потребителей: терминальные (`collect`, `sum`, `count`, `fold`, `max`, `any`, `all`, `find`, `last`), адаптеры (`map`, `filter`, `filter_map`, `flat_map`, `enumerate`, `zip`, `chain`, `skip`, `take`, `take_while`, `skip_while`, `step_by`, `rev`, `peekable`, `cloned`/`copied`) и источники (`iter()`, диапазоны `0..n`, `repeat`, `from_fn`, `successors`).

**Ленивость.** Адаптер лишь создаёт новый итератор; работа выполняется при вызове `next`. Поэтому `take(3)` над бесконечным источником безопасен, а `map` с побочными эффектами без потребителя ничего не делает (компилятор предупредит: *unused ... that must be used*). Итераторы выполняются за один проход и обычно компилируются в тот же код, что и ручной цикл (абстракции с нулевой стоимостью).

**`collect` и вывод типа.** `collect::<B>()` строит любую коллекцию `B: FromIterator`. Тип нужно указать: `let v: Vec<_> = ...` или `collect::<Vec<_>>()`; без него `E0283`. Можно собрать в `String`, `HashMap<K, V>` (из пар), `HashSet`, `BTreeMap`, а из итератора `Result<T, E>` - в `Result<Vec<T>, E>`.

**Владение при итерации.**

| Запись | Тип элемента | Что с коллекцией |
|---|---|---|
| `for x in &v`, `v.iter()` | `&T` | остаётся доступной |
| `for x in &mut v`, `v.iter_mut()` | `&mut T` | заимствована изменяемо |
| `for x in v`, `v.into_iter()` | `T` | перемещена, дальше недоступна (`E0382`) |

Менять размер коллекции внутри цикла по ней нельзя (`E0502`): цикл держит заимствование. Решения: собрать изменения и применить после цикла, `retain`, индексный цикл или `drain`.

**Сортировка и сравнение.** `sort` стабилен и требует `Ord`; для `f64` (только `PartialOrd`) используют `sort_by(|a, b| a.partial_cmp(b).unwrap())` или `total_cmp`. `sort_by_key(|x| ...)` и `Reverse` задают ключ и убывающий порядок.

<!-- section: examples -->
## Примеры

**Пример 1. Vec: срезы, `retain`, `dedup`, `windows`, `chunks`.**

```rust run
fn main() {
    let mut v = vec![5, 3, 8, 3, 5, 5, 9, 1];
    v.sort();
    v.dedup();                            // убирает подряд идущие повторы
    println!("{:?}", v);
    v.retain(|&x| x != 8);
    println!("{:?}", v);

    let pairs: Vec<i32> = v.windows(2).map(|w| w[1] - w[0]).collect();
    println!("{:?}", pairs);
    for chunk in v.chunks(2) {
        print!("{:?} ", chunk);
    }
    println!();
    println!("{:?} {:?}", v.get(1), v.get(99));
    println!("{:?}", v.binary_search(&5));
}
```
```output
[1, 3, 5, 8, 9]
[1, 3, 5, 9]
[2, 2, 4]
[1, 3] [5, 9] 
Some(3) None
Ok(2)
```

**Пример 2. Entry API: группировка и счётчики.**

```rust run
use std::collections::{BTreeMap, HashMap, HashSet};

fn main() {
    let text = "to be or not to be";

    // BTreeMap упорядочен по ключу - вывод детерминирован
    let mut freq: BTreeMap<&str, usize> = BTreeMap::new();
    for w in text.split(' ') {
        *freq.entry(w).or_default() += 1;
    }
    println!("{:?}", freq);

    // Группировка по длине: HashMap<usize, Vec<&str>>
    let mut by_len: HashMap<usize, Vec<&str>> = HashMap::new();
    for w in text.split(' ') {
        by_len.entry(w.len()).or_insert_with(Vec::new).push(w);
    }
    let mut keys: Vec<_> = by_len.keys().copied().collect();
    keys.sort();
    for k in keys {
        println!("{k}: {:?}", by_len[&k]);
    }

    // and_modify + or_insert
    let mut stock: HashMap<&str, i32> = HashMap::new();
    for (item, delta) in [("a", 5), ("b", 2), ("a", -3)] {
        stock.entry(item).and_modify(|n| *n += delta).or_insert(delta);
    }
    println!("a={} b={}", stock["a"], stock["b"]);

    // Множества
    let uniq: HashSet<char> = "mississippi".chars().collect();
    let mut letters: Vec<_> = uniq.into_iter().collect();
    letters.sort();
    println!("{:?}", letters);
}
```
```output
{"be": 2, "not": 1, "or": 1, "to": 2}
2: ["to", "be", "or", "to", "be"]
3: ["not"]
a=2 b=2
['i', 'm', 'p', 's']
```

**Пример 3. Ленивость на деле.**

```rust run
fn main() {
    let v = vec![1, 2, 3, 4, 5, 6];

    let lazy = v.iter().map(|x| {
        println!("map {x}");
        x * 10
    });
    println!("итератор создан, ничего не напечатано");

    let first_two: Vec<i32> = lazy.filter(|x| x % 20 == 0).take(1).collect();
    println!("{:?}", first_two);          // map выполнился только для 1 и 2

    let evens: Vec<u64> = (1..).filter(|n| n % 2 == 0).take(4).collect();   // бесконечный источник
    println!("{:?}", evens);
}
```
```output
итератор создан, ничего не напечатано
map 1
map 2
[20]
[2, 4, 6, 8]
```

**Пример 4. `iter`, `iter_mut`, `into_iter` и владение.**

```rust run
fn main() {
    let mut names = vec![String::from("ada"), String::from("bob")];

    for n in &names {                       // &String
        print!("{} ", n.len());
    }
    println!();

    for n in names.iter_mut() {             // &mut String
        n.push('!');
    }

    let total: usize = names.iter().map(|n| n.len()).sum();
    println!("{total}");

    let owned: Vec<String> = names.into_iter().map(|n| n.to_uppercase()).collect();
    // names перемещён в into_iter: дальше он недоступен
    println!("{:?}", owned);
}
```
```output
3 3 
8
["ADA!", "BOB!"]
```

**Пример 5. `collect` в разные типы и `Result`.**

```rust run
use std::collections::{HashMap, HashSet};

fn main() {
    let s: String = ['р', 'а', 'с', 'т'].iter().collect();
    let squares: HashMap<u32, u32> = (1..=3).map(|n| (n, n * n)).collect();
    let set: HashSet<i32> = [1, 2, 2, 3].into_iter().collect();
    let mut sorted: Vec<_> = squares.iter().collect();
    sorted.sort();
    println!("{s} {:?} {}", sorted, set.len());

    let ok: Result<Vec<i32>, _> = "1 2 3".split(' ').map(str::parse::<i32>).collect();
    let bad: Result<Vec<i32>, _> = "1 x 3".split(' ').map(str::parse::<i32>).collect();
    println!("{:?} {}", ok, bad.is_err());

    let (even, odd): (Vec<i32>, Vec<i32>) = (1..=6).partition(|n| n % 2 == 0);
    println!("{:?} {:?}", even, odd);
}
```
```output
раст [(1, 1), (2, 4), (3, 9)] 3
Ok([1, 2, 3]) true
[2, 4, 6] [1, 3, 5]
```

**Пример 6. Адаптеры в связке: `zip`, `enumerate`, `flat_map`, `fold`, `peekable`.**

```rust run
fn main() {
    let names = ["ada", "bob", "cy"];
    let ages = [36, 41, 29];

    let rows: Vec<String> = names.iter().zip(ages.iter()).enumerate()
        .map(|(i, (n, a))| format!("{}:{n}={a}", i + 1))
        .collect();
    println!("{}", rows.join(", "));

    let letters: Vec<char> = names.iter().flat_map(|n| n.chars()).collect();
    println!("{}", letters.iter().collect::<String>());

    let product = (1..=5).fold(1u64, |acc, x| acc * x);
    let oldest = names.iter().zip(ages).max_by_key(|&(_, a)| a).map(|(n, _)| *n);
    println!("{product} {:?}", oldest);

    // peekable: смотреть вперёд, не потребляя
    let mut it = "112233".chars().peekable();
    let mut groups = Vec::new();
    while let Some(c) = it.next() {
        let mut n = 1;
        while it.peek() == Some(&c) {
            it.next();
            n += 1;
        }
        groups.push((c, n));
    }
    println!("{:?}", groups);
}
```
```output
1:ada=36, 2:bob=41, 3:cy=29
adabobcy
120 Some("bob")
[('1', 2), ('2', 2), ('3', 2)]
```

Ошибки компилятора (`rustc 1.99`) вокруг итерации:

```
error[E0382]: borrow of moved value: `v`        // for s in v {..}; потом v.len()
error[E0502]: cannot borrow `v` as mutable because it is also borrowed as immutable   // v.push в for x in &v
error[E0283]: type annotations needed            // collect() без типа
```

<!-- section: mistakes -->
## Типичные ошибки

* **Полагаться на порядок `HashMap`.** Он меняется от запуска к запуску; сортируйте ключи или используйте `BTreeMap`.
* **`for x in v`, а потом использовать `v`.** Цикл поглощает вектор (`E0382`); пишите `for x in &v`.
* **Менять коллекцию во время обхода** (`E0502`). Накопите изменения в отдельном векторе или используйте `retain`.
* **Забыть тип у `collect()`.** Нужна аннотация: `let v: Vec<_> = ...` или `collect::<Vec<_>>()`.
* **`map` ради побочных эффектов.** Без потребителя он ничего не выполнит; для эффектов есть `for` и `for_each`.
* **`v[i]` на недоверенном индексе.** Паника при выходе за границу; используйте `get(i)`.
* **Клонировать каждый элемент, чтобы обойти заимствование.** Чаще достаточно `iter()`, `cloned()` лишь там, где копия нужна.
* **Вызывать `sort()` на `Vec<f64>`.** У `f64` нет `Ord` из-за `NaN`: используйте `sort_by(|a, b| a.total_cmp(b))`.
* **Дважды искать ключ** (`contains_key`, затем `insert`/`get`). Одно обращение к `entry` быстрее и короче.

<!-- section: summary -->
## Итоги

* `Vec` - последовательность, `HashMap`/`HashSet` - быстрый поиск без порядка, `BTreeMap`/`BTreeSet` - упорядоченные по ключу.
* Entry API (`or_insert`, `or_default`, `and_modify`) заменяет «проверить и вставить» одним поиском.
* Итераторы ленивы: работа начинается в потребителе (`collect`, `sum`, `for`); адаптеры можно безопасно цеплять к бесконечным источникам.
* `iter()` даёт `&T`, `iter_mut()` - `&mut T`, `into_iter()` - `T` и поглощает коллекцию.
* `collect` требует известного целевого типа и умеет собирать `Vec`, `String`, `HashMap` и `Result<Vec<_>, _>`.

Сравните со [срезами и картами в Go](lesson:go.fundamentals.maps-and-slices-deep-dive): там порядок обхода карты тоже случайный, а в Rust ещё и владение определяет, можно ли использовать коллекцию после цикла.
