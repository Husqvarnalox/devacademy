<!-- section: simple -->
## Бесстрашная конкурентность

В большинстве языков гонки данных находят тестами и удачей. В Rust система владения и два маркерных трейта, `Send` и `Sync`, превращают большинство ошибок многопоточности в **ошибки компиляции**. Девиз «fearless concurrency» означает: если код скомпилировался, в нём нет гонок данных (логические ошибки вроде взаимных блокировок остаются возможными).

Основные инструменты:

* `thread::spawn` запускает поток; `JoinHandle::join` ждёт его и возвращает результат.
* `move`-замыкание передаёт потоку владение данными.
* `Arc<T>` - атомарный счётчик ссылок, `Rc` для потоков; `Arc<Mutex<T>>` - общее изменяемое состояние.
* `mpsc::channel` - канал: потоки обмениваются **сообщениями**, а не общей памятью.
* `thread::scope` - потоки, которым разрешено заимствовать локальные данные.

```rust run
use std::sync::{Arc, Mutex};
use std::thread;

fn main() {
    let counter = Arc::new(Mutex::new(0));
    let mut handles = Vec::new();

    for _ in 0..4 {
        let counter = Arc::clone(&counter);
        handles.push(thread::spawn(move || {
            for _ in 0..1000 {
                *counter.lock().unwrap() += 1;
            }
        }));
    }
    for h in handles {
        h.join().unwrap();
    }
    println!("итог = {}", *counter.lock().unwrap());
}
```
```output
итог = 4000
```

Вывод детерминирован: мьютекс гарантирует, что инкременты не потеряются, как бы ни чередовались потоки.

<!-- section: precise -->
## Конкурентность точно

**Потоки.** `thread::spawn(f)` возвращает `JoinHandle<T>`, где `T` - результат замыкания. Замыкание должно быть `'static` и `Send`: оно не может ссылаться на локальные переменные (`E0373`), поэтому данные передают с `move`. После `move` исходная переменная недоступна (`E0382`). Если поток паникует, `join()` возвращает `Err`, а родитель продолжает работу. Если `JoinHandle` отбросить, поток **отсоединяется** и может быть прерван при завершении `main`.

**`Send` и `Sync`.** Это автоматические маркерные трейты:

* `T: Send` - значение можно передать (переместить) в другой поток;
* `T: Sync` - на значение можно делиться ссылкой `&T` между потоками (то есть `&T: Send`).

`Rc<T>` не `Send` и не `Sync` (счётчик неатомарный), `RefCell<T>` не `Sync`; `Arc<T>` - `Send` и `Sync`, когда `T: Send + Sync`; `Mutex<T>` - `Sync`, если `T: Send`. Для `thread::spawn` компилятор проверяет `Send` у замыкания и получает `E0277`, если внутри `Rc`.

**`Mutex<T>`.** Хранит данные внутри. `lock()` блокирует поток и возвращает `LockResult<MutexGuard<T>>`; защитник разыменовывается как `&mut T` и разблокирует мьютекс при `Drop`. Если поток паниковал, держа блокировку, мьютекс помечается **отравленным** (*poisoned*) и `lock()` возвращает `Err`. Держите защитника как можно меньше: долгое удержание сериализует потоки, а повторная блокировка того же мьютекса в том же потоке - взаимная блокировка. `RwLock<T>` разрешает много читателей или одного писателя.

**Каналы `mpsc`.** `let (tx, rx) = mpsc::channel();` - много производителей, один потребитель. `tx.send(v)` перемещает значение в канал (после отправки использовать его нельзя - `E0382`), `rx.recv()` блокируется до сообщения и возвращает `Err`, когда **все** отправители уничтожены; `rx` итерируется (`for m in rx`) до закрытия канала. Отправителей клонируют: `tx.clone()`. Забытый исходный `tx` не даёт каналу закрыться - частая причина зависания.

**`thread::scope`.** Потоки внутри `thread::scope(|s| { s.spawn(|| ...); })` гарантированно завершаются до выхода из `scope`, поэтому могут заимствовать локальные переменные без `Arc` и `move`. Изменяемое заимствование одной переменной двумя потоками остаётся ошибкой (`E0499`), поэтому данные делят на части (`chunks`, `split_at_mut`).

**Атомики.** `AtomicUsize`, `AtomicBool` и другие из `std::sync::atomic` - неблокирующие счётчики и флаги с параметром упорядочения `Ordering` (для простого счётчика достаточно `Relaxed`).

**Выбор подхода.**

| Задача | Инструмент |
|---|---|
| Раздать независимые куски данных | `thread::scope` + `chunks` |
| Собрать результаты от многих потоков | `mpsc::channel` |
| Общий счётчик или кэш | `Arc<Mutex<T>>`, `Arc<RwLock<T>>`, атомики |
| Очередь задач для пула | `Arc<Mutex<Receiver<Job>>>` |

Порядок выполнения потоков **не определён**: любые тесты и вывод должны не зависеть от планировщика (сортируйте результаты по индексу, не печатайте из потоков).

<!-- section: examples -->
## Примеры

**Пример 1. `spawn`, `move` и результат потока.**

```rust run
use std::thread;

fn main() {
    let data = vec![1, 2, 3, 4];
    let handle = thread::spawn(move || {
        let s: i32 = data.iter().sum();
        (data.len(), s)                          // результат вернётся через join
    });
    // data здесь недоступна: перемещена в поток
    let (n, s) = handle.join().unwrap();
    println!("n={n} sum={s}");

    let bad = thread::spawn(|| {
        if true {
            panic!("boom");
        }
        7
    });
    println!("поток паниковал: {}", bad.join().is_err());
}
```
```output
n=4 sum=10
поток паниковал: true
```

**Пример 2. Каналы: несколько производителей, один потребитель.**

```rust run
use std::sync::mpsc;
use std::thread;

fn main() {
    let (tx, rx) = mpsc::channel();
    for id in 0..3 {
        let tx = tx.clone();                     // у каждого потока свой отправитель
        thread::spawn(move || {
            tx.send((id, id * id)).unwrap();
        });
    }
    drop(tx);                                    // иначе rx никогда не закроется

    let mut got: Vec<(i32, i32)> = rx.iter().collect();
    got.sort();                                  // порядок прихода не определён
    println!("{:?}", got);
}
```
```output
[(0, 0), (1, 1), (2, 4)]
```

**Пример 3. `thread::scope`: заимствование без `Arc`.**

```rust run
use std::thread;

fn main() {
    let data = vec![1, 2, 3, 4, 5, 6, 7, 8];
    let mut sums = Vec::new();

    thread::scope(|s| {
        let handles: Vec<_> = data
            .chunks(3)                           // потоки заимствуют куски data
            .map(|chunk| s.spawn(move || chunk.iter().sum::<i32>()))
            .collect();
        for h in handles {
            sums.push(h.join().unwrap());
        }
    });
    println!("{:?} total={}", sums, sums.iter().sum::<i32>());
}
```
```output
[6, 15, 15] total=36
```

**Пример 4. Изменение разных частей среза без блокировок.**

```rust run
use std::thread;

fn main() {
    let mut v = vec![1, 2, 3, 4, 5, 6];
    let (left, right) = v.split_at_mut(3);       // два непересекающихся &mut
    thread::scope(|s| {
        s.spawn(|| left.iter_mut().for_each(|x| *x *= 10));
        s.spawn(|| right.iter_mut().for_each(|x| *x += 1));
    });
    println!("{:?}", v);
}
```
```output
[10, 20, 30, 5, 6, 7]
```

**Пример 5. `Arc<Mutex<..>>`, `RwLock` и атомики.**

```rust run
use std::sync::atomic::{AtomicUsize, Ordering};
use std::sync::{Arc, Mutex, RwLock};
use std::thread;

fn main() {
    let log = Arc::new(Mutex::new(Vec::new()));
    let hits = Arc::new(AtomicUsize::new(0));
    let config = Arc::new(RwLock::new(String::from("v1")));

    let hs: Vec<_> = (0..4)
        .map(|i| {
            let (log, hits, config) = (Arc::clone(&log), Arc::clone(&hits), Arc::clone(&config));
            thread::spawn(move || {
                hits.fetch_add(1, Ordering::Relaxed);
                let c = config.read().unwrap().clone();
                log.lock().unwrap().push(format!("{c}-{i}"));
            })
        })
        .collect();
    for h in hs {
        h.join().unwrap();
    }
    *config.write().unwrap() = "v2".to_string();

    let mut l = log.lock().unwrap().clone();
    l.sort();
    println!("{:?} hits={} cfg={} refs={}", l, hits.load(Ordering::Relaxed), config.read().unwrap(), Arc::strong_count(&log));
}
```
```output
["v1-0", "v1-1", "v1-2", "v1-3"] hits=4 cfg=v2 refs=1
```

**Пример 6. Пул из двух рабочих и общая очередь.**

```rust run
use std::sync::{mpsc, Arc, Mutex};
use std::thread;

fn main() {
    let (job_tx, job_rx) = mpsc::channel::<(usize, u64)>();
    let (res_tx, res_rx) = mpsc::channel::<(usize, u64)>();
    let job_rx = Arc::new(Mutex::new(job_rx));   // Receiver один: делим его под мьютексом

    let workers: Vec<_> = (0..2)
        .map(|_| {
            let job_rx = Arc::clone(&job_rx);
            let res_tx = res_tx.clone();
            thread::spawn(move || loop {
                let job = job_rx.lock().unwrap().recv();   // защитник живёт только до конца выражения
                match job {
                    Ok((i, n)) => res_tx.send((i, n * n)).unwrap(),
                    Err(_) => break,                       // канал закрыт: работы больше нет
                }
            })
        })
        .collect();
    drop(res_tx);

    for (i, n) in [3u64, 4, 5, 6].into_iter().enumerate() {
        job_tx.send((i, n)).unwrap();
    }
    drop(job_tx);                                          // сигнал завершения для рабочих

    let mut results: Vec<_> = res_rx.iter().collect();
    results.sort();
    for w in workers {
        w.join().unwrap();
    }
    println!("{:?}", results);
}
```
```output
[(0, 9), (1, 16), (2, 25), (3, 36)]
```

Ошибки компилятора (`rustc 1.99`):

```
error[E0373]: closure may outlive the current function, but it borrows `v`   // spawn(|| ... v) без move
error[E0382]: borrow of moved value: `v`        // v использована после move в поток
error[E0382]: borrow of moved value: `s`        // s использована после tx.send(s)
error[E0277]: `Rc<i32>` cannot be sent between threads safely   // Rc в thread::spawn
```

<!-- section: mistakes -->
## Типичные ошибки

* **Замыкание без `move`** в `thread::spawn` (`E0373`): поток может пережить функцию. Добавьте `move` или используйте `thread::scope`.
* **Использовать переменную после передачи в поток или канал** (`E0382`). Клонируйте `Arc` заранее и перемещайте клон.
* **Забыть закрыть канал.** Оставшийся оригинальный `tx` не даёт `rx.iter()` завершиться: сделайте `drop(tx)` после раздачи клонов.
* **Не вызвать `join`.** Поток может не успеть выполниться до конца `main`, а паника в нём остаётся незамеченной.
* **Держать `MutexGuard` долго или блокировать дважды.** Повторный `lock()` в том же потоке - взаимная блокировка. Блокируйте на короткое выражение.
* **Рассчитывать на порядок потоков.** Вывод из потоков и порядок сообщений не определены; собирайте результаты с индексом и сортируйте.
* **`unwrap()` на `lock()` без мысли об отравлении.** После паники держателя блокировка отравлена; решите, пропускать ли данные (`into_inner` у `PoisonError`) или падать.
* **Брать `Arc<Mutex<..>>` там, где достаточно `scope`.** Для независимых кусков данных блокировки лишние.
* **Делить `Rc` или `RefCell` между потоками** (`E0277`): нужны `Arc` и `Mutex`.

<!-- section: summary -->
## Итоги

* Безопасность потоков проверяется на этапе компиляции через `Send`, `Sync`, владение и `move`.
* `Arc<Mutex<T>>` - общее изменяемое состояние; `mpsc` - передача сообщений; `thread::scope` - заимствование локальных данных без `Arc`.
* Канал закрывается, когда уничтожены все отправители; лишний живой `tx` приводит к зависанию.
* Порядок выполнения потоков не определён: делайте вывод независимым от планировщика.
* Компилятор не ловит взаимные блокировки и логические ошибки порядка операций - их нужно проектировать.

Сравните с [горутинами и каналами в Go](lesson:go.fundamentals.goroutines-and-channels): девиз Go - «не общайтесь через общую память», в Rust обе модели доступны, но компилятор следит, чтобы общая память не вызывала гонок.
