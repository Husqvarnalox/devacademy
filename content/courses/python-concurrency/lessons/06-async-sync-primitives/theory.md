<!-- section: simple -->
## Порядок в асинхронной толпе

Когда в `asyncio` одновременно работают сотни задач, нужны правила движения - как и в потоках, только устроенные иначе. Три главных инструмента:

* **`asyncio.Lock`** - «в кабинке один человек». Нужен там, где задача читает общее состояние, на чём-то ждёт (`await`), а потом пишет. Пока она ждёт, другие задачи успевают вклиниться, и без замка обновления теряются. Это удивляет тех, кто думает «поток один - гонок нет».
* **`asyncio.Semaphore`** - «в зале не более N человек». Это основной способ ограничить параллелизм: не открыть тысячу соединений сразу, не завалить чужой сервер запросами, уложиться в лимит API.
* **`asyncio.Queue`** - «лента на конвейере». Одни задачи кладут работу, другие (воркеры) её берут. Если сделать ленту ограниченной (`maxsize`), быстрый производитель сам притормаживает, когда воркеры не успевают, - это **обратное давление**.

Важное отличие от потоков ([«threading»](lesson:python.concurrency.threading-and-synchronization)): примитивы asyncio блокируют только *корутину*, а не поток, и работают только внутри своего event loop. Из других потоков ими пользоваться нельзя. Об отмене и ошибках в задачах - [предыдущий урок](lesson:python.concurrency.cancellation-and-timeouts).

<!-- section: precise -->
## Примитивы asyncio

**Где возможна гонка.** Между двумя `await` задача выполняется без вмешательства других задач цикла, поэтому `self.n += 1` без `await` внутри безопасно *в одной корутине*. Но если критическая секция содержит `await` (запрос в сеть, `sleep`, `queue.get`), другие задачи выполняются в середине секции. Правило: защищаемый инвариант не должен «разрываться» на `await`, иначе нужен `Lock` (или переработка кода).

**Lock.** `async with lock:` - вход и выход; `await lock.acquire()` / `lock.release()` - ручное управление; `lock.locked()`. Не рекурсивный (повторный вход из той же задачи взаимно заблокирует её). Задачи ждут в порядке FIFO. Не потокобезопасен.

**Semaphore / BoundedSemaphore.** `asyncio.Semaphore(n)`; `async with sem:` пропускает не более `n` задач одновременно. `BoundedSemaphore` не даёт `release()` больше, чем `acquire()`. Типичный шаблон лимита: создайте `sem` один раз, оберните в `async with sem:` только саму операцию (вызов `fetch`), а не весь код обработки. Помните: семафор ограничивает *одновременность*, а не частоту; для ограничения «запросов в секунду» нужен отдельный механизм (например, корутина-планировщик с `sleep`).

**Event, Condition.** `Event` - флаг: `set()`, `clear()`, `await wait()`; идеален для детерминированной синхронизации порядка (в том числе в тестах). `Condition` - ожидание изменения состояния под блокировкой (`async with cond: await cond.wait_for(pred)`).

**Barrier (3.11+).** `asyncio.Barrier(parties)`: все ждут `await barrier.wait()`, пока не соберутся `parties` задач; удобно для тестов и «стартового выстрела».

**Queue.** `asyncio.Queue(maxsize=0)`: `await put(x)` (ждёт места, если очередь ограничена), `await get()` (ждёт элемента), `put_nowait`/`get_nowait` (поднимают `QueueFull`/`QueueEmpty`), `qsize()`, `task_done()`, `await join()`. Есть `PriorityQueue` и `LifoQueue`. С Python 3.13 появился `shutdown()`.

**Пул воркеров над очередью.**

1. создать ограниченную очередь;
2. запустить N воркеров: `while True: item = await q.get(); try: ... finally: q.task_done()`;
3. производитель делает `await q.put(item)` (это место, где работает обратное давление);
4. после загрузки: `await q.join()` - вернётся, когда на каждый `put` вызван `task_done`;
5. воркеры теперь блокируются на `get()`: их останавливают отменой (`task.cancel()`; в `TaskGroup` выход из блока дождётся) или sentinel-значением (`None` на воркера).

Ошибку обработки отдельного элемента нужно перехватывать в воркере: иначе воркер умрёт, `task_done` не вызовется и `join()` зависнет. Именно поэтому `task_done` кладут в `finally`.

**Измерение конкурентности без часов.** Чтобы проверить, что лимит соблюдается, ведите счётчик «сейчас внутри» и его максимум; в обработчике делайте несколько `await asyncio.sleep(0)`, чтобы другие задачи успели войти в защищаемый блок. Результат (`пик == лимиту`) детерминирован и не зависит от скорости машины.

**Потоки и asyncio.** Примитивы asyncio не потокобезопасны; для передачи данных между потоком и циклом используйте `loop.call_soon_threadsafe`, `asyncio.run_coroutine_threadsafe` (следующий урок) или потокобезопасную `queue.Queue` вместе с `to_thread`.

<!-- section: examples -->
## Примеры

**Гонка в однопоточном asyncio и её лечение.** Без замка десять задач читают `0`, и итог `1`. Результат детерминирован: порядок переключений определяется циклом.

```python run
import asyncio

class Counter:
    def __init__(self, locked):
        self.value = 0
        self._lock = asyncio.Lock() if locked else None

    async def _add(self):
        current = self.value
        await asyncio.sleep(0)       # переключение в середине «чтение-запись»
        self.value = current + 1

    async def add(self):
        if self._lock is None:
            await self._add()
        else:
            async with self._lock:
                await self._add()

async def run(locked):
    c = Counter(locked)
    await asyncio.gather(*(c.add() for _ in range(10)))
    return c.value

print("без замка:", asyncio.run(run(False)))
print("с замком:", asyncio.run(run(True)))
```
```output
без замка: 1
с замком: 10
```

**Semaphore ограничивает параллелизм, счётчик это доказывает.**

```python run
import asyncio

async def main():
    sem = asyncio.Semaphore(3)
    now = peak = 0

    async def job(i):
        nonlocal now, peak
        async with sem:
            now += 1
            peak = max(peak, now)
            for _ in range(3):
                await asyncio.sleep(0)
            now -= 1

    await asyncio.gather(*(job(i) for i in range(10)))
    print("пик одновременных:", peak)

asyncio.run(main())
```
```output
пик одновременных: 3
```

**Воркеры над ограниченной очередью.** Производитель блокируется на `put`, когда очередь заполнена. Результаты собираются по индексу.

```python run
import asyncio

async def main():
    queue = asyncio.Queue(maxsize=2)
    results = {}
    log = []

    async def worker():
        while True:
            i = await queue.get()
            try:
                await asyncio.sleep(0)
                results[i] = i * i
            finally:
                queue.task_done()

    async with asyncio.TaskGroup() as tg:
        workers = [tg.create_task(worker()) for _ in range(2)]
        for i in range(6):
            await queue.put(i)
            log.append(queue.qsize())
        await queue.join()
        for w in workers:
            w.cancel()
    print([results[i] for i in range(6)])
    print("очередь не превышала 2:", max(log) <= 2)

asyncio.run(main())
```
```output
[0, 1, 4, 9, 16, 25]
очередь не превышала 2: True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Считать, что в asyncio гонок нет.** Любой `await` внутри «прочитал - изменил - записал» открывает окно для гонки.
* **Захватывать семафор вокруг всего кода обработки**, включая тяжёлые вычисления и ожидания, не относящиеся к лимитируемому ресурсу: пропускная способность падает.
* **Создавать `Semaphore`/`Lock` на каждый вызов.** Лимит работает только на общем экземпляре.
* **Забыть `task_done()`.** `join()` не вернётся никогда. Всегда `finally: queue.task_done()`.
* **Не останавливать воркеров.** После `join()` они висят на `get()`; без отмены или sentinel программа не завершается или вызывает предупреждения о ждущих задачах.
* **Неограниченная очередь.** Без `maxsize` быстрый производитель съест память; обратное давление отсутствует.
* **Использовать asyncio-примитивы из потока.** Они не потокобезопасны и привязаны к циклу.
* **Принимать `Semaphore` за rate limit.** Он ограничивает одновременные операции, но не их частоту.
* **Исключение в воркере без обработки.** Воркер умирает, элемент теряется, `join()` зависает (или `TaskGroup` отменяет всех).

<!-- section: summary -->
## Итоги

* `await` внутри критической секции даёт гонку даже в одном потоке: защищайте её `asyncio.Lock`.
* `asyncio.Semaphore(n)` ограничивает число одновременных операций; пик измеряйте счётчиком.
* `asyncio.Queue(maxsize=N)` + воркеры = конвейер с обратным давлением; `task_done` в `finally`, `join()` для ожидания, отмена воркеров в конце.
* Ошибки отдельного элемента перехватывайте в воркере.
* Примитивы asyncio не потокобезопасны.

<!-- section: facts -->
## Факты

* `asyncio.Barrier` и `asyncio.timeout` добавлены в Python 3.11.
* У `asyncio.Queue` с Python 3.13 есть `shutdown()`, поднимающий `QueueShutDown` в ожидающих.
