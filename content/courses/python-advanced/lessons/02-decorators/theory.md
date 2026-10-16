<!-- section: simple -->
## Декоратор - функция, которая улучшает другую функцию

В [прошлом уроке](lesson:python.advanced.functions-as-objects) мы выяснили: функция - объект, который можно передать и вернуть, а вложенная функция запоминает переменные внешней. **Декоратор** соединяет эти две идеи: это функция, которая принимает функцию и возвращает новую (обычно - обёртку вокруг исходной).

Запись

```python
@timed
def work(): ...
```

означает ровно то же, что `work = timed(work)`. Знак `@` - только удобный синтаксис. Декораторы нужны, когда одно и то же поведение надо «приклеить» ко многим функциям, не меняя их код: замерять время, повторять вызов при ошибке, запоминать результаты, проверять права, писать журнал.

Пример по аналогии: у вас есть посылка (функция). Декоратор - это упаковка вокруг неё. Получатель вызывает упаковку; та что-то делает до, открывает посылку (вызывает исходную функцию) и что-то делает после.

Две типичные трудности. Первая: после декорирования теряются имя и документация функции, если не применить `functools.wraps`. Вторая: декоратор с параметрами (`@retry(3)`) требует лишнего уровня вложенности, и поначалу это сбивает.

<!-- section: precise -->
## Точная механика

**Применение.** Инструкция `def` с декораторами выполняется так: сначала создаётся обычный объект функции, затем к нему применяются декораторы **снизу вверх**, и результат привязывается к имени. Для

```python
@a
@b
def f(): ...
```

получается `f = a(b(f))`. Выражение после `@` вычисляется обычным образом, поэтому `@retry(3)` означает: `deco = retry(3)`, затем `f = deco(f)`. Отсюда три уровня вложенности для декоратора с аргументами: внешняя функция принимает параметры, средняя - декорируемую функцию, внутренняя - вызовы.

**Обёртка.** Типичная обёртка принимает `*args, **kwargs` и передаёт их дальше, чтобы не зависеть от сигнатуры. Она возвращает результат исходной функции. Забытый `return` превращает результат в `None`.

**Порядок вызова при стекировании.** Применяются снизу вверх, но при вызове внешний декоратор (верхний) срабатывает первым: его обёртка вызывает обёртку нижнего, а та - исходную функцию. Код «до» выполняется сверху вниз, код «после» - снизу вверх.

**functools.wraps.** Без него `decorated.__name__` равно `"wrapper"`, а `__doc__` - докстрингу обёртки. `@functools.wraps(func)` над обёрткой копирует `__module__`, `__name__`, `__qualname__`, `__doc__`, обновляет `__dict__` и записывает исходную функцию в `__wrapped__`. Через `__wrapped__` работают `inspect.signature` и `inspect.unwrap`, а для отладки и справки функция остаётся узнаваемой.

**Состояние обёртки.** Хранить счётчики и кеши можно в замыкании (`nonlocal`) или в атрибутах самой обёртки (`wrapper.calls`). Атрибут удобнее: он виден снаружи.

**Кеширование.** Мемоизация - сохранение результатов чистой функции по аргументам. В стандартной библиотеке для неё есть `functools.lru_cache(maxsize=128)` (ограничивает размер и вытесняет давно не использованные записи) и `functools.cache` (Python 3.9+, без ограничения). Ключ кеша строится из аргументов, поэтому они должны быть **хешируемыми** (числа, строки, кортежи из хешируемых элементов). Передача списка даёт `TypeError: unhashable type: 'list'`. Кеширование осмысленно только для детерминированных функций без побочных эффектов: иначе вы вернёте устаревший результат. `cache_info()` показывает попадания и промахи, `cache_clear()` очищает кеш. Для методов экземпляра кеш хранит ссылки на `self`, что может задерживать удаление объектов - будьте осторожны.

**Декораторы классов** работают так же: функция принимает класс и возвращает класс (обычно тот же, изменённый). `@dataclass` из [следующих уроков](lesson:python.advanced.dunder-methods-and-dataclasses) - именно такой декоратор.

<!-- section: examples -->
## Примеры

**Пример 1. Обёртка и functools.wraps.**

```python run
import functools

def plain(func):
    def wrapper(*args, **kwargs):
        return func(*args, **kwargs)
    return wrapper

def good(func):
    @functools.wraps(func)
    def wrapper(*args, **kwargs):
        return func(*args, **kwargs)
    return wrapper

@plain
def a():
    """Док a."""

@good
def b():
    """Док b."""

print(a.__name__, a.__doc__)
print(b.__name__, b.__doc__)
print(b.__wrapped__.__name__)
```
```output
wrapper None
b Док b.
b
```

**Пример 2. Практические декораторы: замер времени и повтор.**

```python run
import functools
import time

def timed(func):
    @functools.wraps(func)
    def wrapper(*args, **kwargs):
        start = time.perf_counter()
        try:
            return func(*args, **kwargs)
        finally:
            wrapper.last_elapsed = time.perf_counter() - start
    wrapper.last_elapsed = None
    return wrapper

def retry(times, exceptions=(Exception,)):
    def decorator(func):
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            for attempt in range(1, times + 1):
                try:
                    return func(*args, **kwargs)
                except exceptions as exc:
                    print(f"попытка {attempt} не удалась: {exc}")
                    if attempt == times:
                        raise
        return wrapper
    return decorator

state = {"calls": 0}

@retry(3, exceptions=(ConnectionError,))
@timed
def fetch():
    state["calls"] += 1
    if state["calls"] < 3:
        raise ConnectionError("нет связи")
    return "данные"

print(fetch())
print(fetch.__name__, state["calls"])
```
```output
попытка 1 не удалась: нет связи
попытка 2 не удалась: нет связи
данные
fetch 3
```

**Пример 3. Порядок при двух декораторах.**

```python run
import functools

def announce(label):
    def decorator(func):
        print(f"применяю {label}")
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            print(f"вход {label}")
            result = func(*args, **kwargs)
            print(f"выход {label}")
            return result
        return wrapper
    return decorator

@announce("A")
@announce("B")
def work():
    print("работа")

print("--- вызов")
work()
```
```output
применяю B
применяю A
--- вызов
вход A
вход B
работа
выход B
выход A
```

**Пример 4. Мемоизация: вручную и через functools.**

```python run
import functools

@functools.lru_cache(maxsize=None)
def fib(n):
    return n if n < 2 else fib(n - 1) + fib(n - 2)

print(fib(30))
print(fib.cache_info())

@functools.cache
def total(items):
    return sum(items)

print(total((1, 2, 3)))
try:
    total([1, 2, 3])
except TypeError as exc:
    print("TypeError:", exc)

def register(cls):
    cls.registered = True
    return cls

@register
class Plugin:
    pass

print(Plugin.registered)
```
```output
832040
CacheInfo(hits=28, misses=31, maxsize=None, currsize=31)
6
TypeError: unhashable type: 'list'
True
```

<!-- section: mistakes -->
## Типичные ошибки

* **Забыли `functools.wraps`.** Имя, докстринг и сигнатура пропадают; в трассировках везде `wrapper`. Ставьте `@wraps(func)` в каждую обёртку.
* **Забыли `return` в обёртке** - декорированная функция начинает возвращать `None`.
* **`@retry` вместо `@retry(3)`.** Декоратору с аргументами нужны скобки. Без них `retry` получит функцию в роли `times`, и ошибка проявится только при вызове.
* **Не передали `*args, **kwargs`.** Обёртка с фиксированной сигнатурой ломается при другом наборе аргументов.
* **Кеш для функции с побочными эффектами или зависящей от времени/состояния.** Вы получите «замороженный» результат. Кешируйте только чистые функции.
* **Нехешируемые аргументы у `lru_cache`/`cache`.** Список, словарь, множество дают `TypeError`. Преобразуйте в кортеж или `frozenset`.
* **Неограниченный `cache` для бесконечного потока разных аргументов** - утечка памяти. Используйте `lru_cache(maxsize=N)`.
* **Путаница порядка стекирования.** Применяется снизу вверх, а внешний декоратор срабатывает при вызове первым.

<!-- section: summary -->
## Итог

* `@d` над `def f` - то же, что `f = d(f)`; несколько декораторов применяются снизу вверх.
* Обёртка принимает `*args, **kwargs`, вызывает исходную функцию и возвращает её результат; метаданные сохраняет `@functools.wraps`.
* Декоратор с аргументами - три уровня: параметры, функция, вызов.
* `lru_cache` и `cache` кешируют по хешируемым аргументам; годятся только для чистых функций.
* Декоратор класса - это функция «класс в класс».
* Дальше: [итераторы и генераторы](lesson:python.advanced.iterators-and-generators) и [контекстные менеджеры](lesson:python.advanced.context-managers), где декораторы тоже встретятся (`@contextmanager`).
