<!-- section: simple -->
## Конкурентность с границами

Вспомните, как устроены обычные функции: вызвал - дождался - получил результат или ошибку. Всё, что функция порождает внутри, заканчивается до её возврата. **Структурная конкурентность** переносит эту простую гарантию на параллельно выполняющиеся задачи: задача, запущенная внутри блока, не переживёт блок, а её ошибка не потеряется, а дойдёт до того, кто её запустил. В `asyncio` это `TaskGroup`.

Сравните «огонь и забвение»: `asyncio.create_task(job())` без ссылки. Задача живёт сама по себе, её ошибка выведется в лог в лучшем случае при сборке мусора, а при остановке программы никто не знает, что она ещё работает. Структурный подход проще рассуждать: если блок вышел, внутри ничего не осталось.

Из этого вырастает **graceful shutdown** - предсказуемая остановка службы: перестать брать новую работу, дать закончить текущую, сохранить остаток и закрыть ресурсы. Отдельная тема - **мост между потоками и event loop**: синхронный код (GUI, библиотека, поток из пула) должен вызывать корутины, а корутина - блокирующий код, и делать это нужно только предусмотренными для этого функциями. И, наконец, **отладка**: у asyncio есть режим, подсказывающий о забытых `await` и о блокирующих вызовах в цикле.

Это итоговый урок курса: он опирается на [задачи и TaskGroup](lesson:python.concurrency.asyncio-basics), [отмену](lesson:python.concurrency.cancellation-and-timeouts), [примитивы и очереди](lesson:python.concurrency.async-sync-primitives) и [пулы потоков](lesson:python.concurrency.concurrent-futures).

<!-- section: precise -->
## Проектирование

**Правила структурной конкурентности.**

1. Каждая конкурентная задача принадлежит блоку: `async with asyncio.TaskGroup() as tg:`. Выход из блока означает, что все дочерние задачи завершены (успехом, ошибкой или отменой).
2. Ошибка дочерней задачи отменяет остальных и поднимается как `ExceptionGroup`; обработка - `except*` на границе, где вы знаете, что делать.
3. Отмена родителя отменяет детей; детям нужно корректно выполнять `finally` и не подавлять `CancelledError`.
4. Вложенность свободно комбинируется: внутри задачи группы можно открыть свою группу. Время жизни образует дерево.
5. Если «фоновая» работа действительно должна пережить запрос (например, отправка письма), её передают долгоживущей службе (очередь и воркеры), а не создают `create_task` в обработчике.

**Служба с воркерами и graceful shutdown.** Типовая схема: очередь, N воркеров в `TaskGroup`, сигнал остановки. Воркер бесконечно ждёт либо следующего элемента, либо сигнала остановки; после сигнала новые элементы не берутся, взятые доводятся до конца, остаток очереди сохраняется или возвращается вызывающему. Ожидание «элемент или остановка» делается через `asyncio.wait({get_task, stop_task}, return_when=FIRST_COMPLETED)` с обязательной отменой проигравшей задачи в `finally`. Ограничьте время остановки: `async with asyncio.timeout(...)` вокруг ожидания воркеров, а по истечении - отмена.

**Сигналы операционной системы.** На POSIX `loop.add_signal_handler(signal.SIGTERM, stop.set)` позволяет превратить сигнал в `asyncio.Event`; это основа остановки службы по `SIGTERM`/`SIGINT`. На Windows этот метод не поддерживается. При использовании `asyncio.run` (через `asyncio.Runner`, Python 3.11+) нажатие Ctrl+C отменяет главную задачу, что даёт работающим корутинам шанс выполнить `finally`.

```python
# фрагмент для POSIX; в учебной песочнице не запускается
async def main():
    stop = asyncio.Event()
    loop = asyncio.get_running_loop()
    for sig in (signal.SIGINT, signal.SIGTERM):
        loop.add_signal_handler(sig, stop.set)
    await serve(queue, handler, workers=4, stop=stop)
```

**Потоки и event loop.** Цикл и его объекты (`Task`, `Event`, `Queue`) принадлежат своему потоку. Правильные мосты:

* **Из потока в цикл, ожидая результат:** `fut = asyncio.run_coroutine_threadsafe(coro, loop)`, затем `fut.result(timeout)` - блокирует *вызывающий поток* (не поток цикла!). Результат - `concurrent.futures.Future`; у него есть `cancel()`. Вызывать `result()` из самого потока цикла нельзя: взаимная блокировка.
* **Из потока в цикл, без ожидания:** `loop.call_soon_threadsafe(callback, *args)` - единственный потокобезопасный способ поставить обычный вызов в цикл (например, `stop.set`).
* **Из цикла в поток:** `await asyncio.to_thread(func, *args)` для блокирующих вызовов (копирует `contextvars`) или `loop.run_in_executor(executor, func, *args)` для своего пула (процессов в том числе). Внутри такой функции нельзя трогать asyncio-объекты напрямую.
* **Постоянный мост:** фоновый поток, в котором работает `asyncio.run(...)` и ждёт сигнала остановки; из других потоков в него отправляют корутины. По завершении `asyncio.run` отменяет оставшиеся задачи и закрывает цикл. Так делают синхронные обёртки над асинхронными библиотеками.
* Несколько циклов в разных потоках допустимы, но у каждого свои объекты; передавайте данные через потокобезопасные структуры, а не делитесь `asyncio.Queue`.

Поток с блокирующим кодом лишь освобождает цикл. Он по-прежнему подчиняется GIL (см. [«Модели конкурентности»](lesson:python.concurrency.concurrency-models)): для CPU-bound работы выбирайте процессы.

**Отладка.**

* **Режим отладки:** `asyncio.run(main(), debug=True)`, `PYTHONASYNCIODEBUG=1` или `python -X dev`. Включает дополнительные проверки: предупреждение о корутинах, которые не были ожидаемы (с указанием места создания), ошибку при вызове потоконебезопасных API не из потока цикла, и логирование (в логгер `asyncio`) шагов задач и callback, выполняющихся дольше `loop.slow_callback_duration` (по умолчанию 0.1 с) - главный способ найти блокирующий вызов в корутине.
* **Не пропущенные исключения:** если исключение задачи никто не забрал, при её удалении логируется «Task exception was never retrieved». Такой лог - признак «огня и забвения».
* **Инспекция задач:** `asyncio.all_tasks()` (используется и в тестах, чтобы проверить отсутствие висящих задач), `task.get_stack()`, `task.get_name()` (имена задаёт параметр `name=`). Начиная с Python 3.14, к работающему процессу можно подключиться извне: `python -m asyncio ps PID` и `python -m asyncio pstree PID` покажут таблицу задач и дерево ожидающих корутин.
* **Тестирование без часов:** вместо `sleep` синхронизируйтесь событиями и счётчиками; проверяйте пик конкурентности и отсутствие висящих задач.

<!-- section: examples -->
## Примеры

**Блок TaskGroup как граница.** После выхода из блока не осталось дочерних задач, а результаты доступны.

```python run
import asyncio

async def job(i):
    await asyncio.sleep(0)
    return i * i

async def main():
    async with asyncio.TaskGroup() as tg:
        tasks = [tg.create_task(job(i), name=f"job-{i}") for i in range(3)]
    print([t.result() for t in tasks])
    print("активных задач кроме главной:", len(asyncio.all_tasks()) - 1)

asyncio.run(main())
```
```output
[0, 1, 4]
активных задач кроме главной: 0
```

**Graceful shutdown.** Две воркера берут работу, сигнал остановки выставляется из обработчика; начатые элементы дорабатывают, остальное возвращается как остаток. Вывод не зависит от времени: порядок задаёт цикл.

```python run
import asyncio

async def serve(queue, stop):
    done = []

    async def worker():
        while not stop.is_set():
            item = await queue.get()
            if item == 2:
                stop.set()                 # сигнал остановки из обработчика
            await asyncio.sleep(0)          # «работа»
            done.append(item)

    async with asyncio.TaskGroup() as tg:
        tg.create_task(worker())
    rest = []
    while not queue.empty():
        rest.append(queue.get_nowait())
    return done, rest

async def main():
    queue, stop = asyncio.Queue(), asyncio.Event()
    for i in range(6):
        queue.put_nowait(i)
    print(await serve(queue, stop))

asyncio.run(main())
```
```output
([0, 1, 2], [3, 4, 5])
```

**Поток вызывает корутину в чужом цикле.** Цикл живёт в фоновом потоке; основной поток отправляет туда работу и ждёт `concurrent.futures.Future`.

```python run
import asyncio
import threading

loop = asyncio.new_event_loop()
thread = threading.Thread(target=loop.run_forever, name="loop-thread")
thread.start()

async def where(x):
    await asyncio.sleep(0)
    return x + 1, threading.current_thread().name

future = asyncio.run_coroutine_threadsafe(where(41), loop)
print(future.result(timeout=2))

loop.call_soon_threadsafe(loop.stop)    # остановить цикл можно только так
thread.join()
loop.close()
print("поток завершён:", not thread.is_alive())
```
```output
(42, 'loop-thread')
поток завершён: True
```

**Режим отладки находит блокирующий вызов.** Пороговое значение занижено для демонстрации; блокирующий `time.sleep` внутри корутины превышает его, и asyncio жалуется в логгер `asyncio`.

```python run
import asyncio
import logging
import time

records = []

class Capture(logging.Handler):
    def emit(self, record):
        records.append(record.getMessage())

logging.getLogger("asyncio").addHandler(Capture())

async def blocker():
    time.sleep(0.05)          # блокирующий вызов в корутине

async def main():
    asyncio.get_running_loop().slow_callback_duration = 0.01
    await blocker()

asyncio.run(main(), debug=True)
print("предупреждение о медленном callback:", any("took" in r for r in records))
```
```output
предупреждение о медленном callback: True
```

<!-- section: mistakes -->
## Типичные ошибки

* **`create_task` без ссылки и без группы.** Потерянная задача и потерянное исключение; предпочитайте `TaskGroup`.
* **Выход из программы без остановки службы.** Обрыв посреди обработки теряет данные; используйте сигнал остановки и дождитесь воркеров.
* **Опрашивающий цикл `while queue.empty(): await sleep(...)`.** Ждите событие: `queue.get()` или `asyncio.wait` на `get` и `stop`.
* **Не отменять проигравшую задачу `asyncio.wait`.** Остаются висящие `get()` и `stop.wait()`; в `finally` их отменяют.
* **Вызвать `fut.result()` из потока цикла.** Взаимная блокировка: цикл ждёт сам себя.
* **Трогать asyncio-объекты из чужого потока** (`loop.create_task`, `event.set()`, `queue.put_nowait`). Это не потокобезопасно; используйте `call_soon_threadsafe` или `run_coroutine_threadsafe`.
* **Блокирующий код без `to_thread`.** Цикл замирает, все таймауты срабатывают с опозданием; режим отладки покажет медленный callback.
* **Ждать, что `to_thread` ускорит вычисления.** Поток освобождает цикл, но не обходит GIL.
* **Поймать `ExceptionGroup` обычным `except`.** Нужен `except*` (см. урок про отмену).
* **Оставить режим отладки включённым в продакшене.** Он замедляет цикл и шумит в логах.

<!-- section: summary -->
## Итоги

* Структурная конкурентность: задачи живут в блоке `TaskGroup`, ошибки доходят до родителя, отмена каскадируется.
* Graceful shutdown = сигнал остановки + доработка текущего + возврат остатка + отсутствие висящих задач.
* Между потоками и циклом ходят только `run_coroutine_threadsafe`, `call_soon_threadsafe`, `to_thread`, `run_in_executor`.
* Постоянный мост: фоновый поток с `asyncio.run`, остановка через событие, отмена остатка при выходе.
* Отладка: `debug=True`/`PYTHONASYNCIODEBUG=1`/`-X dev`, `slow_callback_duration`, `all_tasks()`, в 3.14 `python -m asyncio ps|pstree`.

<!-- section: facts -->
## Факты

* `asyncio.Runner` и `TaskGroup` добавлены в Python 3.11.
* `loop.add_signal_handler` недоступен на Windows.
