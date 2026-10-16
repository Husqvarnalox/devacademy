<!-- section: simple -->
## Как правильно остановить задачу

В реальных программах дела приходится прерывать: пользователь закрыл вкладку, запрос превысил срок, соседняя задача упала и результат уже не нужен. В `asyncio` для этого есть **отмена**: у задачи вызывают `task.cancel()`, и в ближайшем месте, где она ждала (`await`), возникает специальное исключение `CancelledError`. Задача может им воспользоваться, чтобы прибраться - закрыть соединение, снять блокировку, откатить действие, - и должна дать исключению подняться дальше. Это не ошибка, а сигнал «тебя остановили».

Главная привычка: **не глотайте `CancelledError`**. Если вы поймаете его и «успокоите», вышестоящий код (таймаут, `TaskGroup`, завершение программы) будет считать, что задача ещё работает, и всё зависнет или сломается. Прибираться нужно в `finally`: он выполнится и при успехе, и при ошибке, и при отмене.

С отменой тесно связаны **таймауты**: «подожди не дольше N секунд, иначе отмени». А ещё нужно уметь *защитить* короткую критичную операцию (например, фиксацию транзакции) от отмены, пришедшей снаружи, и разбирать *несколько ошибок сразу*, которые приносит `TaskGroup`.

Основы задач разобраны в [«asyncio: event loop, корутины и задачи»](lesson:python.concurrency.asyncio-basics).

<!-- section: precise -->
## Механика отмены

**CancelledError.** `task.cancel()` планирует поднятие `CancelledError` внутри корутины в точке её текущего `await` (если задача ещё не стартовала, она завершится отменённой, не начав работу). Если задача уже завершена, `cancel()` вернёт `False` и ничего не сделает. `CancelledError` наследует `BaseException`, а не `Exception`: широкий `except Exception` его не перехватит - и это намеренно. `task.cancelled()` истинно, только если задача **завершилась** отменённой; сразу после `cancel()` оно ещё ложно, пока задача не обработала сигнал. Чтобы дождаться завершения, делают `await task` и перехватывают `CancelledError` (или используют `asyncio.gather(..., return_exceptions=True)`).

**Очистка.** Шаблон:

```python
async def worker():
    resource = await open_resource()
    try:
        await do_work(resource)
    finally:
        await resource.close()   # выполнится и при отмене
```

В `finally` при отмене можно делать `await`, но помните: если во время очистки придёт ещё одна отмена, она прервёт и очистку. Если очистка должна пройти обязательно, ограничьте её таймаутом или защитите `shield`. Ловить `CancelledError` ради логирования допустимо, но тогда обязательно `raise`.

**Таймауты.**

* `async with asyncio.timeout(seconds):` (Python 3.11+) - ограничивает время всего блока. По истечении времени операция внутри отменяется, а на выходе из блока отмена превращается в `TimeoutError` (в 3.11+ `asyncio.TimeoutError` - алиас встроенного `TimeoutError`). Срок можно сдвинуть: `cm.reschedule(...)`; `asyncio.timeout_at(when)` принимает абсолютное время цикла; `seconds=None` отключает ограничение.
* `await asyncio.wait_for(aw, timeout)` - ограничение одного awaitable; по истечении отменяет его и поднимает `TimeoutError`.
* Контекстный менеджер различает две причины остановки: таймаут (станет `TimeoutError`) и отмена извне (останется `CancelledError`). Поэтому ловите `TimeoutError`, а не `CancelledError`.

**shield.** `await asyncio.shield(aw)` разрывает связь отмены: если внешнюю задачу отменят, ожидающая сторона получит `CancelledError`, но внутренняя задача продолжит выполнение. Это инструмент для коротких критичных шагов. Нужно сохранить ссылку на внутреннюю задачу (`inner = asyncio.ensure_future(...)`) и, при необходимости, дождаться её после отмены (`await inner`). Не используйте `shield` как общий способ сделать задачи неубиваемыми: при остановке цикла и закрытии программы внутренняя задача всё равно будет прервана.

**Каскад отмены в TaskGroup.** Если задача группы завершилась ошибкой (не `CancelledError`), `TaskGroup` отменяет все остальные, дожидается их завершения и поднимает `ExceptionGroup` со всеми собранными ошибками. Если же отменили саму родительскую задачу, находящуюся в `async with TaskGroup()`, группа тоже отменит всех детей. `CancelledError`, возникшая из-за этих отмен, в группу ошибок не попадает.

**ExceptionGroup и except\*.** `ExceptionGroup(msg, [exc1, exc2, ...])` (PEP 654, Python 3.11+) - исключение, содержащее несколько исключений. Обычный `except ValueError` его не поймает, поэтому для групп есть форма **`except*`**:

```python
try:
    async with asyncio.TaskGroup() as tg: ...
except* ValueError as eg:
    ...   # eg - ExceptionGroup только с ValueError
except* (KeyError, TypeError) as eg:
    ...
```

Каждая ветка `except*` получает *подгруппу* соответствующих исключений; несовпавшие продолжают распространяться (в виде новой группы). Ветки выполняются все, где есть совпадения, а не только первая. В `except*` нельзя использовать `return`, `break` и `continue`. Методы группы: `.exceptions` (кортеж), `.split(predicate_or_type)`, `.subgroup(...)`. Если `TaskGroup` завершилась ошибкой всего одной задачи, всё равно приходит `ExceptionGroup` из одного элемента.

<!-- section: examples -->
## Примеры

**Очистка при отмене.** Задача отменена, `finally` выполнился, `CancelledError` дошёл до вызывающего.

```python run
import asyncio

async def worker(log, started):
    log.append("открыть")
    try:
        started.set()
        await asyncio.Event().wait()      # ждём вечно
    finally:
        log.append("закрыть")

async def main():
    log, started = [], asyncio.Event()
    task = asyncio.create_task(worker(log, started))
    await started.wait()
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        print("отменена:", task.cancelled())
    print(log)

asyncio.run(main())
```
```output
отменена: True
['открыть', 'закрыть']
```

**Таймаут против отмены извне.** Внутренняя операция в обоих случаях получает `CancelledError`, но снаружи мы видим разное.

```python run
import asyncio

async def slow():
    await asyncio.Event().wait()

async def with_timeout():
    try:
        async with asyncio.timeout(0.01):
            await slow()
    except TimeoutError:
        return "TimeoutError"

async def main():
    print(await with_timeout())
    task = asyncio.create_task(with_timeout())
    await asyncio.sleep(0)
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        print("внешняя отмена осталась CancelledError")

asyncio.run(main())
```
```output
TimeoutError
внешняя отмена осталась CancelledError
```

**Каскад в TaskGroup и except\*.** Две задачи падают с разными ошибками, третья ждёт и отменяется. Барьер гарантирует, что обе ошибки возникают до того, как группа начнёт отмену.

```python run
import asyncio

async def main():
    barrier = asyncio.Barrier(3)
    log = []

    async def fail(exc):
        await barrier.wait()
        raise exc

    async def waiter():
        try:
            await barrier.wait()
            await asyncio.Event().wait()
        except asyncio.CancelledError:
            log.append("waiter отменён")
            raise

    try:
        async with asyncio.TaskGroup() as tg:
            tg.create_task(fail(ValueError("v")))
            tg.create_task(fail(KeyError("k")))
            tg.create_task(waiter())
    except* ValueError as eg:
        print("ValueError:", [str(e) for e in eg.exceptions])
    except* KeyError as eg:
        print("KeyError:", [repr(e) for e in eg.exceptions])
    print(log)

asyncio.run(main())
```
```output
ValueError: ['v']
KeyError: ["KeyError('k')"]
['waiter отменён']
```

**shield.** Отмена внешнего ожидания не прерывает фиксацию.

```python run
import asyncio

async def main():
    log = []
    release = asyncio.Event()

    async def commit():
        await release.wait()
        log.append("зафиксировано")

    inner = asyncio.ensure_future(commit())

    async def outer():
        await asyncio.shield(inner)

    task = asyncio.create_task(outer())
    await asyncio.sleep(0)
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        log.append("внешняя задача отменена")
    release.set()
    await inner
    print(log, "inner отменена:", inner.cancelled())

asyncio.run(main())
```
```output
['внешняя задача отменена', 'зафиксировано'] inner отменена: False
```

<!-- section: mistakes -->
## Типичные ошибки

* **Глотание отмены через голый `except:` или `except BaseException`.** Они ловят `CancelledError`; если не поднять его снова, таймауты и `TaskGroup` перестанут работать.
* **Поймать `CancelledError` и вернуть значение.** Вызывающий решит, что всё хорошо. Либо `raise`, либо не ловите.
* **Ждать `TimeoutError` от `CancelledError`.** Таймаут `asyncio.timeout` превращается в `TimeoutError`; отмена извне остаётся `CancelledError`. Не смешивайте.
* **Очистка без защиты.** `await` в `finally` может быть прерван повторной отменой; для критичной очистки используйте `shield` или короткий таймаут.
* **Использовать `gather` и ждать отмены соседей.** `gather` не отменяет остальные задачи при ошибке; нужен `TaskGroup`.
* **`except ValueError` для ошибок из `TaskGroup`.** Приходит `ExceptionGroup`; обычный `except` его не поймает. Нужен `except*`.
* **`shield` без ссылки на внутреннюю задачу.** Результат и исключения защищённой работы будут потеряны.
* **`return` внутри `except*`.** Это синтаксическая ошибка; сохраните результат в переменную и вернитесь после блока.

<!-- section: summary -->
## Итоги

* Отмена - это `CancelledError` в точке `await`; очищайте в `finally` и всегда пробрасывайте её дальше.
* `asyncio.timeout` отменяет операцию и превращает отмену в `TimeoutError`; внешняя отмена остаётся `CancelledError`.
* `asyncio.shield` защищает короткую критичную операцию от отмены ожидающей стороны, но не делает её бессмертной.
* `TaskGroup` при первой ошибке отменяет остальных и поднимает `ExceptionGroup`; разбирайте её через `except*`.

<!-- section: facts -->
## Факты

* `CancelledError` наследуется от `BaseException` (с Python 3.8), поэтому `except Exception` его не ловит.
* `asyncio.timeout`, `TaskGroup`, `ExceptionGroup` и `except*` появились в Python 3.11.
