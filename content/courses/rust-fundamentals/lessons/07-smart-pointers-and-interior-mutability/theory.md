<!-- section: simple -->
## Умные указатели и внутренняя изменяемость

Обычное правило Rust - один владелец и проверка заимствований на этапе компиляции. Умные указатели расширяют его в тех местах, где одного владельца мало:

* `Box<T>` - значение в куче с **одним** владельцем. Нужен для рекурсивных типов и для `dyn Trait`.
* `Rc<T>` - **несколько владельцев** в одном потоке. Данные освобождаются, когда уходит последний `Rc`.
* `RefCell<T>` - изменение через общую ссылку: правила заимствования проверяются **во время выполнения**, а не компиляции.
* `Weak<T>` - ссылка, которая не владеет данными; ею разрывают циклы `Rc`.

Типичная связка для общего изменяемого состояния в одном потоке - `Rc<RefCell<T>>`.

```rust run
use std::cell::RefCell;
use std::rc::Rc;

fn main() {
    let shared = Rc::new(RefCell::new(vec![1, 2]));
    let other = Rc::clone(&shared);          // не копирует вектор, увеличивает счётчик

    other.borrow_mut().push(3);              // меняем через общую ссылку
    println!("{:?}", shared.borrow());
    println!("владельцев: {}", Rc::strong_count(&shared));

    drop(other);
    println!("владельцев: {}", Rc::strong_count(&shared));
}
```
```output
[1, 2, 3]
владельцев: 2
владельцев: 1
```

Цена гибкости - перенос ошибок из компиляции в выполнение: нарушение правил заимствования у `RefCell` приводит к панике.

<!-- section: precise -->
## Умные указатели точно

**Умный указатель** - структура, ведущая себя как ссылка (`Deref`) и управляющая ресурсом (`Drop`). Автоматическое разыменование (*deref coercion*) превращает `&Box<String>` в `&str`, когда это требуется по типу параметра.

**`Box<T>`.** Хранит `T` в куче; при выходе из области освобождает её. Размер самого `Box` - один указатель, поэтому им разрывают бесконечный размер рекурсивных типов: `enum List { Cons(i32, Box<List>), Nil }`. Без `Box` ошибка `E0072`. `Box<dyn Trait>` - владеющий trait-объект.

**`Rc<T>`.** Счётчик ссылок без атомарных операций. `Rc::clone(&a)` лишь увеличивает счётчик (дёшево). Внутри только неизменяемый доступ: `Rc<String>` нельзя изменить (`E0596`). `Rc::strong_count`, `Rc::try_unwrap`, `Rc::get_mut` (работает только при единственном владельце), `Rc::make_mut` (клонирует при общем владении). `Rc` **не `Send`**: передать его в другой поток нельзя (`E0277`), для потоков есть `Arc` (урок про конкурентность).

**Внутренняя изменяемость (*interior mutability*).** Типы, позволяющие менять данные через `&T`:

* `Cell<T>` - для `Copy`-значений: `get` и `set`, без ссылок на содержимое.
* `RefCell<T>` - `borrow()` -> `Ref<T>`, `borrow_mut()` -> `RefMut<T>`; счётчик активных заимствований ведётся в рантайме. Несколько `Ref` или один `RefMut`, но не одновременно. Нарушение - паника `RefCell already borrowed` (для `borrow()` при активном `RefMut` текст `RefCell already mutably borrowed`); безопасные варианты - `try_borrow` и `try_borrow_mut`.
* `Mutex` и `RwLock` - то же для потоков.

Защитники `Ref` и `RefMut` освобождаются при `Drop`, поэтому временные заимствования внутри одного выражения безопасны, а долгоживущая переменная-защитник - частая причина паники.

**Циклы и `Weak<T>`.** Два `Rc`, ссылающиеся друг на друга, никогда не обнулят счётчик: утечка памяти (в safe-коде это допустимо, но нежелательно). `Rc::downgrade(&rc)` создаёт `Weak<T>`, не увеличивающий сильный счётчик; `weak.upgrade()` возвращает `Option<Rc<T>>` (`None`, если владельцев уже нет). Классический пример - дерево: родитель владеет детьми (`Rc`), ребёнок ссылается на родителя слабо (`Weak`).

**Порядок освобождения.** Когда `strong_count` падает до нуля, значение внутри `Rc` уничтожается (вызывается `Drop`); сама память блока освобождается, когда исчезнет и последний `Weak`.

**Трейты `Deref` и `Drop`.** `Deref` позволяет писать `*boxed` и применять методы внутреннего типа. `Drop::drop` вызывается автоматически; вызвать его вручную нельзя, для раннего освобождения служит `drop(x)` из прелюдии.

| Тип | Владельцев | Изменение | Проверка |
|---|---|---|---|
| `Box<T>` | 1 | через `&mut` | компиляция |
| `Rc<T>` | много | нет | компиляция |
| `Rc<RefCell<T>>` | много | да | рантайм |
| `Weak<T>` | 0 (наблюдатель) | нет | `upgrade()` |

<!-- section: examples -->
## Примеры

**Пример 1. `Box` и рекурсивный тип.**

```rust run
#[derive(Debug)]
enum List {
    Cons(i32, Box<List>),
    Nil,
}
use List::{Cons, Nil};

fn sum(list: &List) -> i32 {
    match list {
        Cons(v, rest) => v + sum(rest),     // &Box<List> автоматически становится &List
        Nil => 0,
    }
}

fn main() {
    let list = Cons(1, Box::new(Cons(2, Box::new(Cons(3, Box::new(Nil))))));
    println!("{:?}", list);
    println!("sum = {}", sum(&list));
    println!("размер Box<List> = {} байт", std::mem::size_of::<Box<List>>());
}
```
```output
Cons(1, Cons(2, Cons(3, Nil)))
sum = 6
размер Box<List> = 8 байт
```

**Пример 2. `Rc`: счётчик и порядок освобождения.**

```rust run
use std::rc::Rc;

struct Noisy(&'static str);
impl Drop for Noisy {
    fn drop(&mut self) {
        println!("drop {}", self.0);
    }
}

fn main() {
    let a = Rc::new(Noisy("data"));
    println!("count = {}", Rc::strong_count(&a));
    {
        let b = Rc::clone(&a);
        let c = Rc::clone(&a);
        println!("count = {}", Rc::strong_count(&a));
        drop(b);
        drop(c);
    }
    println!("count = {}", Rc::strong_count(&a));
    drop(a);
    println!("конец main");
}
```
```output
count = 1
count = 3
count = 1
drop data
конец main
```

**Пример 3. `Cell`, `RefCell` и проверка правил в рантайме.**

```rust run
use std::cell::{Cell, RefCell};

fn main() {
    let c = Cell::new(5);
    c.set(c.get() + 1);
    println!("cell = {}", c.get());

    let r = RefCell::new(vec![1]);
    {
        let a = r.borrow();
        let b = r.borrow();                     // несколько читателей допустимы
        println!("{} {}", a.len(), b.len());
        println!("try_borrow_mut: {}", r.try_borrow_mut().is_err());
    }
    r.borrow_mut().push(2);
    println!("{:?}", r);

    let _w = r.borrow_mut();
    // второй borrow_mut() здесь запаниковал бы; try_ вариант возвращает Err
    println!("второй borrow_mut: {}", r.try_borrow_mut().is_err());
    println!("borrow: {}", r.try_borrow().is_err());
}
```
```output
cell = 6
1 1
try_borrow_mut: true
RefCell { value: [1, 2] }
второй borrow_mut: true
borrow: true
```

Если вместо `try_borrow_mut` вызвать `borrow_mut()` при активном `_w`, программа завершится паникой `RefCell already borrowed` (код 101).

**Пример 4. Общий граф владения: `Rc<RefCell<..>>`.**

```rust run
use std::cell::RefCell;
use std::rc::Rc;

#[derive(Debug)]
struct Account {
    balance: i64,
}

fn main() {
    let acc = Rc::new(RefCell::new(Account { balance: 100 }));
    let owners: Vec<Rc<RefCell<Account>>> = (0..3).map(|_| Rc::clone(&acc)).collect();

    for (i, o) in owners.iter().enumerate() {
        o.borrow_mut().balance += (i as i64 + 1) * 10;
    }
    println!("{:?}", acc.borrow());
    println!("владельцев: {}", Rc::strong_count(&acc));
}
```
```output
Account { balance: 160 }
владельцев: 4
```

**Пример 5. `Weak`: дерево без утечек.**

```rust run
use std::cell::RefCell;
use std::rc::{Rc, Weak};

struct Node {
    name: String,
    parent: RefCell<Weak<Node>>,
    children: RefCell<Vec<Rc<Node>>>,
}

fn node(name: &str) -> Rc<Node> {
    Rc::new(Node {
        name: name.to_string(),
        parent: RefCell::new(Weak::new()),
        children: RefCell::new(vec![]),
    })
}

fn main() {
    let root = node("root");
    let leaf = node("leaf");
    *leaf.parent.borrow_mut() = Rc::downgrade(&root);
    root.children.borrow_mut().push(Rc::clone(&leaf));

    println!("родитель листа: {:?}", leaf.parent.borrow().upgrade().map(|p| p.name.clone()));
    println!("root: strong={} weak={}", Rc::strong_count(&root), Rc::weak_count(&root));
    println!("leaf: strong={} weak={}", Rc::strong_count(&leaf), Rc::weak_count(&leaf));
    println!("детей у root: {}", root.children.borrow().len());

    drop(root);
    println!("после drop(root): {:?}", leaf.parent.borrow().upgrade().map(|p| p.name.clone()));
}
```
```output
родитель листа: Some("root")
root: strong=1 weak=1
leaf: strong=2 weak=0
детей у root: 1
после drop(root): None
```

Ошибки компилятора (`rustc 1.99`):

```
error[E0596]: cannot borrow data in an `Rc` as mutable        // a.push('y') для Rc<String>
error[E0277]: `Rc<i32>` cannot be sent between threads safely   // thread::spawn с Rc
error[E0072]: recursive type `List` has infinite size           // Cons(i32, List) без Box
```

<!-- section: mistakes -->
## Типичные ошибки

* **Рекурсивный тип без `Box`** (`E0072`): компилятору нужен конечный размер; оберните рекурсивное поле в `Box`.
* **Ждать изменяемости от `Rc<T>`** (`E0596`): внутри `Rc` общий доступ, изменять нужно через `RefCell` или `Rc::make_mut`.
* **`Rc` между потоками** (`E0277`): `Rc` не `Send`; используйте `Arc`.
* **Паника `RefCell already borrowed`.** Защитник `borrow_mut()` лежит в переменной, а ниже вызывается другой `borrow()`. Держите защитников в узкой области `{ ... }` или вызывайте `drop(guard)`.
* **Цикл `Rc` -> `Rc`** приводит к утечке: счётчик не обнуляется. Обратные связи (родитель, наблюдатель) делайте через `Weak`.
* **`Rc::clone` путают с глубокой копией.** Клонируется только указатель, данные остаются общими; для копии данных используйте `(*rc).clone()`.
* **`RefCell` там, где хватило бы `&mut`.** Внутренняя изменяемость переносит проверки в рантайм; применяйте её, когда структура владения иначе не выражается.
* **`Rc::try_unwrap` на общем значении.** Вернёт `Err(rc)`, если есть другие владельцы.

<!-- section: summary -->
## Итоги

* `Box` - единственный владелец в куче (рекурсивные типы, `dyn Trait`), `Rc` - много владельцев в одном потоке.
* `Cell` и `RefCell` позволяют менять данные через `&T`; `RefCell` проверяет правила заимствования в рантайме и паникует при нарушении.
* `Rc<RefCell<T>>` - стандартный способ общего изменяемого состояния в однопоточном коде.
* `Weak` не владеет данными и разрывает циклы; `upgrade()` возвращает `Option`.
* Счётчики видны через `strong_count` и `weak_count`; значение уничтожается, когда обнуляется сильный счётчик.

Сравните с [общим владением в C++ (RAII и значения)](lesson:cpp.first-steps.value-semantics-references-and-raii): `Rc` похож на `std::shared_ptr`, а `Weak` - на `std::weak_ptr`, но в Rust ошибки изменяемости превращаются либо в ошибку компиляции, либо в контролируемую панику.
