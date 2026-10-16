<!-- section: simple -->
## Пул работников вместо ручных потоков

Создавать потоки вручную (`Thread`, `join`, очередь результатов) утомительно и легко ошибиться. Модуль `concurrent.futures` даёт более высокий уровень: вы описываете *что* сделать, а **пул** (executor) сам решает, какие работники это выполнят. Вы отправляете задачу и сразу получаете **Future** - «расписку» на будущий результат. Когда результат понадобится, вы просите его у Future: если задача уже готова, получите сразу, если нет - подождёте. Если задача упала с ошибкой, ошибка спрятана в Future и всплывёт в тот момент, когда вы потребуете результат.

Есть два пула с одинаковым интерфейсом. `ThreadPoolExecutor` - потоки: хорош для блокирующего ввода-вывода. `ProcessPoolExecutor` - процессы: нужен для вычислений на нескольких ядрах, но за изоляцию памяти платит сериализацией: всё, что вы отправляете и получаете, копируется через `pickle`.

Идея та же, что в [«Модели конкурентности»](lesson:python.concurrency.concurrency-models): выбор пула - это выбор между потоками и процессами. Сами потоки, замки и очереди разобраны в [«threading»](lesson:python.concurrency.threading-and-synchronization).

<!-- section: precise -->
## Executor и Future

**Executor.** `with ThreadPoolExecutor(max_workers=N) as pool:` - выход из блока вызывает `shutdown(wait=True)`: новые задачи не принимаются, а блок ждёт завершения уже отправленных. Основные методы:

* `submit(fn, *args, **kwargs) -> Future` - поставить одну задачу.
* `map(fn, *iterables, timeout=None, chunksize=1)` - применить функцию ко всем элементам; задачи выполняются параллельно, а итератор отдаёт результаты **в порядке входа**. Если задача упала, исключение поднимется, когда итерация дойдёт до её результата. Для `ProcessPoolExecutor` параметр `chunksize` группирует элементы, снижая накладные расходы.
* `shutdown(wait=True, *, cancel_futures=False)` - явное закрытие.

**Future.** Методы: `result(timeout=None)` (результат или повторно поднятое исключение; `TimeoutError`, если не успело), `exception(timeout=None)` (само исключение или `None`), `done()`, `running()`, `cancelled()`, `cancel()` (получится только пока задача не началась), `add_done_callback(fn)`. Callback вызывается в потоке, завершившем задачу (или сразу, если Future уже готов).

**Ожидание нескольких Future.** `as_completed(futures)` отдаёт Future по мере завершения - хорош для «обработать, что готово». `wait(futures, timeout=None, return_when=ALL_COMPLETED | FIRST_COMPLETED | FIRST_EXCEPTION)` возвращает два множества: выполненные и ожидающие. Чтобы знать, какому входу соответствует Future, храните словарь `{future: ключ}`.

**Исключения.** Они не пересекают границу пула сами: `submit` вернётся нормально. Исключение хранится в Future; `result()` поднимет его заново, `exception()` вернёт объект. Если не вызвать ни то ни другое, ошибка пройдёт незамеченной. Для процессов исключение (как и результат) сериализуется pickle; сломанный пул поднимает `BrokenProcessPool`, когда рабочий процесс аварийно умер.

**ProcessPoolExecutor и сериализация.**

* Функция, аргументы и результат должны пройти через `pickle`. Функции передаются *по имени* (модуль плюс имя), поэтому годятся функции верхнего уровня импортируемого модуля; лямбды, вложенные функции и локальные классы - нет. Открытые файлы, сокеты, замки, генераторы тоже не сериализуются.
* Что быстрее: передать много мелких задач или несколько крупных пачек? Накладные расходы на сериализацию и IPC платятся за каждую передачу, поэтому мелкую работу группируют в крупные части (`chunksize`, функция `chunked`).
* **Методы запуска процессов.** `spawn` запускает чистый интерпретатор и заново импортирует главный модуль - поэтому код, создающий процессы, должен быть под защитой `if __name__ == "__main__":`, иначе каждый дочерний процесс снова создаст пул. `fork` копирует процесс целиком (только POSIX; небезопасен при наличии потоков). `forkserver` порождает процессы из отдельного сервера. Согласно документации Python 3.14: на **Windows и macOS умолчание `spawn`**, на остальных POSIX-платформах (в частности Linux) умолчание `forkserver`; в Python 3.13 и более ранних на Linux было `fork`. Явный выбор: `multiprocessing.get_context("spawn")` и параметр `mp_context` у `ProcessPoolExecutor`. Не опирайтесь на то, что глобальные переменные родителя «просто есть» в дочернем процессе: это работает при `fork`, но не при `spawn`.

**Потоки или процессы.** CPU-bound на обычной сборке: процессы (если вычисления крупнее накладных расходов). I/O-bound с блокирующими библиотеками: потоки. Если данные большие, а работа лёгкая, процессы могут оказаться медленнее обычного цикла из-за копирования.

<!-- section: examples -->
## Примеры

**submit, result и map.** Порядок `map` совпадает со входным.

```python run
from concurrent.futures import ThreadPoolExecutor

def square(x):
    return x * x

with ThreadPoolExecutor(max_workers=3) as pool:
    future = pool.submit(square, 7)
    print(future.result())
    print(list(pool.map(square, [1, 2, 3, 4])))
```
```output
49
[1, 4, 9, 16]
```

**as_completed против порядка входа.** Порядок завершения задан цепочкой событий, а не временем: задача 2 завершается первой, её завершение разрешает завершиться задаче 1, а затем задаче 0. Разрешение выдаётся в callback Future, то есть строго после перехода предыдущего Future в состояние «готов».

```python run
from concurrent.futures import ThreadPoolExecutor, as_completed
import threading

gates = [threading.Event() for _ in range(3)]
gates[2].set()

def task(i):
    gates[i].wait()
    return i

with ThreadPoolExecutor(max_workers=3) as pool:
    futures = [pool.submit(task, i) for i in range(3)]
    for i in (2, 1):
        futures[i].add_done_callback(lambda f, i=i: gates[i - 1].set())
    print("as_completed:", [f.result() for f in as_completed(futures)])
    print("по порядку входа:", [f.result() for f in futures])
```
```output
as_completed: [2, 1, 0]
по порядку входа: [0, 1, 2]
```

**Исключения живут в Future.** `submit` не бросает; ошибка появляется при `result()`. Остальные задачи не страдают.

```python run
from concurrent.futures import ThreadPoolExecutor

def inverse(x):
    return 1 / x

with ThreadPoolExecutor(max_workers=2) as pool:
    futures = {x: pool.submit(inverse, x) for x in (4, 0, 2)}
    for x, future in futures.items():
        exc = future.exception()
        print(x, "ошибка:" if exc else "ok:", type(exc).__name__ if exc else future.result())
```
```output
4 ok: 0.25
0 ошибка: ZeroDivisionError
2 ok: 0.5
```

**Процессы: функция верхнего уровня и защита `__main__`.** Лямбду отправить нельзя - её невозможно сериализовать. Ниже проверка до отправки и обычный запуск пула процессов.

```python run
import pickle
from concurrent.futures import ProcessPoolExecutor

def cube(x):
    return x ** 3

def picklable(obj):
    try:
        pickle.dumps(obj)
    except Exception as exc:
        return type(exc).__name__
    return True

if __name__ == "__main__":
    print("cube:", picklable(cube))
    print("lambda:", picklable(lambda x: x))
    with ProcessPoolExecutor(max_workers=2) as pool:
        print(list(pool.map(cube, range(5))))
```
```output
cube: True
lambda: PicklingError
[0, 1, 8, 27, 64]
```

<!-- section: mistakes -->
## Типичные ошибки

* **Не вызывать `result()`.** Ошибка задачи тихо теряется. Всегда забирайте результат или `exception()`, особенно у «огня и забвения».
* **Ожидать порядка завершения от `map`.** `map` сохраняет порядок входа; для порядка завершения нужен `as_completed`.
* **Лямбды и вложенные функции в `ProcessPoolExecutor`.** Ошибка сериализации; выносите функцию на верхний уровень модуля.
* **Нет `if __name__ == "__main__":`.** При `spawn` (macOS, Windows) дочерний процесс повторно импортирует модуль и рекурсивно создаёт процессы или падает с ошибкой.
* **Много мелких задач в процессах.** Накладные расходы сериализации съедают выигрыш: группируйте.
* **Разделяемое состояние между процессами.** Глобальные переменные и объекты не общие: изменение в дочернем процессе не видно родителю.
* **Пул без `with`.** Если забыть `shutdown`, программа может не завершиться вовремя или оборвать работу.
* **Блокировка пула своими же задачами.** Задача, ждущая результат другой задачи того же заполненного пула, вызывает взаимную блокировку.

<!-- section: summary -->
## Итоги

* `Executor` + `Future`: `submit` для одной задачи, `map` для порядка входа, `as_completed` для порядка завершения.
* Ошибка задачи хранится в Future и поднимается в `result()`; не вызвали - не узнали.
* Пул закрывайте через `with`.
* `ProcessPoolExecutor` требует picklable функции, аргументов и результатов; группируйте мелкие задачи.
* macOS и Windows используют `spawn` по умолчанию, Linux в 3.14 - `forkserver`; защищайте точку входа `if __name__ == "__main__":`.

<!-- section: facts -->
## Факты

* Разные start methods проверяйте на целевой платформе: `multiprocessing.get_start_method()` покажет текущий.
* С Python 3.14 в `concurrent.futures` есть `InterpreterPoolExecutor` (по PEP 734, независимые интерпретаторы в одном процессе); подробности - в «What's New in Python 3.14».
