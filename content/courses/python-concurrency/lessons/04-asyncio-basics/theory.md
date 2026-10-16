<!-- section: simple -->
## Один поток, много дел

В `asyncio` всё крутится вокруг **event loop** (цикла событий): это обычный цикл внутри одного потока, который держит список задач и по очереди даёт каждой поработать. Задача работает, пока не дойдёт до слова `await` - места, где ей нужно подождать (ответ сети, таймер, сигнал от другой задачи). В этот момент она сама отдаёт управление циклу, и тот берёт следующую готовую задачу. Когда ожидаемое случилось, цикл вернёт управление первой задаче ровно туда, где она остановилась.

Функция `async def` называется **корутиной**. Важный сюрприз: её вызов ничего не выполняет, а возвращает *объект корутины* - как рецепт, который ещё не готовили. Чтобы блюдо приготовилось, рецепт нужно либо «выполнить здесь и дождаться» (`await coro()`), либо отдать циклу как отдельное дело - создать **Task** (`asyncio.create_task(coro())`). Несколько Task идут конкурентно: пока одна ждёт, другая работает.

Платой за простоту служит дисциплина: цикл один, и если одна из задач заняла его и не отдаёт (долгое вычисление, `time.sleep`, блокирующий клиент), стоят все. Блокирующие вызовы либо заменяют асинхронными, либо уводят в поток (`asyncio.to_thread`).

Процессы, потоки и корутины сравниваются в уроке [«Модели конкурентности»](lesson:python.concurrency.concurrency-models); отмена и таймауты - в следующем.

<!-- section: precise -->
## Основы asyncio

**Запуск.** `asyncio.run(main())` создаёт event loop, выполняет корутину `main()` до конца и закрывает цикл. Внутри работающего цикла `asyncio.run` вызывать нельзя.

**Корутина, Task, Future.**

* Корутинная функция (`async def`) вызывается и возвращает объект корутины; тело не запущено.
* `await x` приостанавливает *текущую* корутину, пока `x` (корутина, Task, Future или любой awaitable) не завершится, и возвращает его результат или поднимает его исключение. Это единственные точки переключения: между двумя `await` код задачи выполняется без вмешательства других задач.
* `asyncio.create_task(coro)` оборачивает корутину в **Task** и сразу ставит в очередь цикла. Тело Task начнёт выполняться, когда текущая задача отдаст управление (на `await` или завершении). Сохраняйте ссылку на Task: документация предупреждает, что цикл не гарантирует, что она не будет собрана сборщиком мусора до завершения.
* `Task` - подкласс `Future`: у него есть `result()`, `exception()`, `done()`, `cancel()`, `add_done_callback`.

**Порядок выполнения детерминирован.** Очередь готовых задач цикла - FIFO: задачи, созданные подряд, начинают работать в порядке создания, а возобновляются в порядке готовности. Для надёжной координации порядок всё равно задают **синхронизацией** (`asyncio.Event`, очереди), а не рассчитанным `sleep`: на `sleep` нельзя полагаться ни в тестах, ни в продакшене.

**Конкурентный запуск нескольких корутин.**

* `await asyncio.gather(*aws, return_exceptions=False)` запускает все и возвращает список результатов в порядке аргументов. При первой ошибке она пробрасывается наружу, **остальные задачи не отменяются** и продолжают работать. С `return_exceptions=True` исключения возвращаются как результаты.
* `async with asyncio.TaskGroup() as tg: t = tg.create_task(coro())` (Python 3.11+) - **структурная конкурентность**: выход из блока дожидается всех задач. Если одна задача упала, группа отменяет остальные, дожидается их и поднимает `ExceptionGroup` со всеми ошибками (разбор - `except*`, урок про отмену). Результаты берут из `t.result()` после блока.
* `asyncio.wait(tasks, return_when=...)`, `asyncio.as_completed(...)` - когда нужен контроль над порядком готовности.

Практическое правило: используйте `TaskGroup`, если задачи составляют одну логическую операцию и при ошибке остальные не имеют смысла; `gather` - для независимых вызовов, где нужны все результаты (часто с `return_exceptions=True`).

**Блокирующий код.** Любой синхронный вызов, который долго занимает поток, замораживает цикл вместе со всеми задачами и таймерами. Решения: асинхронная версия библиотеки; `await asyncio.to_thread(func, *args)` (Python 3.9+) - выполнить функцию в потоке пула по умолчанию и дождаться её; `loop.run_in_executor` с `ProcessPoolExecutor` для тяжёлых вычислений. `to_thread` копирует контекст (`contextvars`) в поток. Поток освобождает цикл, но не отменяет GIL: чисто вычислительный код в потоке всё равно конкурирует за GIL с циклом.

**Примитивы связи.** `asyncio.Event` (`set`, `wait`, `clear`), `asyncio.Queue`, `asyncio.Lock`, `asyncio.Semaphore` - внутри работают так же, как их потоковые аналоги, но блокируют только корутину и не потокобезопасны (о них - урок про асинхронные примитивы).

<!-- section: examples -->
## Примеры

**Корутина не выполняется при вызове.** Видно, что тело стартует лишь на `await`.

```python run
import asyncio

async def hello():
    print("тело hello началось")
    return "готово"

async def main():
    coro = hello()
    print("корутина создана:", type(coro).__name__)
    print(await coro)

asyncio.run(main())
```
```output
корутина создана: coroutine
тело hello началось
готово
```

**Порядок на `await`.** Две задачи чередуются на каждом `await asyncio.sleep(0)` (это просто «уступить управление»). Порядок полностью определён устройством цикла.

```python run
import asyncio

async def worker(name, steps, log):
    for i in range(steps):
        log.append(f"{name}{i}")
        await asyncio.sleep(0)

async def main():
    log = []
    a = asyncio.create_task(worker("A", 3, log))
    b = asyncio.create_task(worker("B", 2, log))
    await asyncio.gather(a, b)
    print(log)

asyncio.run(main())
```
```output
['A0', 'B0', 'A1', 'B1', 'A2']
```

**gather и TaskGroup при ошибке.** `gather` пробрасывает ошибку, но соседняя задача продолжает жить; `TaskGroup` отменяет соседа и собирает ошибки в `ExceptionGroup`.

```python run
import asyncio

async def failing():
    await asyncio.sleep(0)
    raise ValueError("сбой")

async def sibling(log, tag):
    try:
        await asyncio.sleep(0.05)
        log.append(f"{tag}: завершена")
    except asyncio.CancelledError:
        log.append(f"{tag}: отменена")
        raise

async def with_gather():
    log = []
    try:
        await asyncio.gather(failing(), sibling(log, "gather"))
    except ValueError as exc:
        print("gather:", exc)
    await asyncio.sleep(0.1)        # дать соседу дожить
    print(log)

async def with_group():
    log = []
    try:
        async with asyncio.TaskGroup() as tg:
            tg.create_task(failing())
            tg.create_task(sibling(log, "group"))
    except* ValueError as eg:
        print("group:", type(eg).__name__, [str(e) for e in eg.exceptions])
    print(log)

asyncio.run(with_gather())
asyncio.run(with_group())
```
```output
gather: сбой
['gather: завершена']
group: ExceptionGroup ['сбой']
['group: отменена']
```

**Блокирующий вызов замораживает цикл.** Фоновая задача должна успеть сказать блокирующей функции «хватит ждать». Если функция вызвана прямо в корутине, цикл стоит, фоновая задача не работает, и функция ждёт впустую (0.3 с по таймауту). Через `to_thread` цикл свободен, и функция получает сигнал.

```python run
import asyncio
import threading

def blocking_wait(signal):
    return signal.wait(0.3)          # блокирует поток до сигнала или таймаута

async def run(offload):
    signal = threading.Event()

    async def background():
        await asyncio.sleep(0)       # пока цикл свободен, это успеет отработать
        signal.set()

    task = asyncio.create_task(background())
    if offload:
        got = await asyncio.to_thread(blocking_wait, signal)
    else:
        got = blocking_wait(signal)  # блокирует цикл: background не запустится
    await task
    return got

print("прямо в корутине:", asyncio.run(run(False)))
print("через to_thread:", asyncio.run(run(True)))
```
```output
прямо в корутине: False
через to_thread: True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Забыть `await`.** `fetch()` без `await` создаёт корутину и ничего не делает; ищите предупреждение «coroutine ... was never awaited».
* **Последовательные `await` вместо конкурентности.** `a = await f(); b = await g()` выполняет по очереди. Для конкурентности создайте Task или используйте `gather`/`TaskGroup`.
* **Блокирующий вызов в корутине.** `time.sleep`, `requests`, чтение большого файла, тяжёлый расчёт замораживают весь цикл. Используйте `await asyncio.sleep`, асинхронные клиенты или `asyncio.to_thread`.
* **Потеря ссылки на Task.** `asyncio.create_task(job())` без сохранения результата рискованно: Task может быть собрана до завершения, а его исключение останется незамеченным. Держите ссылку или используйте `TaskGroup`.
* **Положиться на `sleep` для порядка.** Это делает программу хрупкой и тесты медленными. Синхронизируйте через `Event`, очереди и `await` задачи.
* **Считать `gather` отменяющим соседей.** Он не отменяет остальные задачи при ошибке; в этом отличие от `TaskGroup`.
* **Использовать asyncio-примитивы из других потоков.** `asyncio.Event`, `Queue`, `Lock` не потокобезопасны; мост между потоками и циклом - `loop.call_soon_threadsafe` и `run_coroutine_threadsafe` (последний урок курса).

<!-- section: summary -->
## Итоги

* Event loop выполняет одну задачу за раз; переключение только на `await`.
* Вызов `async def` возвращает корутину; выполняют её `await` или `create_task`.
* `gather` собирает результаты (ошибка не отменяет соседей), `TaskGroup` даёт структурную конкурентность и `ExceptionGroup`.
* Блокирующий код выносите: `asyncio.to_thread` или асинхронные библиотеки.
* Порядок задавайте событиями и очередями, а не `sleep`.

<!-- section: facts -->
## Факты

* `asyncio.run` - Python 3.7, `asyncio.to_thread` - 3.9, `asyncio.TaskGroup` и `asyncio.timeout` - 3.11.
* В режиме отладки (`asyncio.run(main(), debug=True)` или `PYTHONASYNCIODEBUG=1`) asyncio предупреждает о слишком долгих callback и забытых `await` (подробнее - в последнем уроке курса).
