<!-- section: simple -->
## Подсказки типов: комментарии для людей и инструментов

В Python можно написать `def add(a: int, b: int) -> int:` и `names: list[str] = []`. Эти пометки называются **аннотациями типов** (type hints). Главное, что нужно понять с первой минуты: **интерпретатор их не проверяет**. Вызов `add("x", "y")` спокойно вернёт `"xy"`, потому что `+` для строк определён. Аннотации - это информация для читателя кода, для редактора (автодополнение) и для отдельных программ-проверяльщиков - **статических анализаторов** вроде mypy и Pyright, которые читают исходный код, не запуская его, и сообщают: «здесь передана строка вместо числа».

Зачем это нужно, если Python не следит? Потому что в больших программах ошибки типов - самый частый источник багов, а подсказки превращают договорённости («эта функция принимает список чисел и возвращает строку или `None`») в проверяемые утверждения. Они же служат документацией, которая не устаревает так быстро, как комментарии.

Важное ограничение учебной среды: **mypy и Pyright здесь не установлены** (их интеграция в платформу запланирована), поэтому настоящую статическую проверку вы в этом курсе не запустите. Зато аннотации можно *прочитать во время выполнения* и написать на их основе собственную проверку, чем мы и займёмся в упражнении. Библиотеки вроде dataclasses и валидаторов данных именно так используют аннотации.

В уроке: современный синтаксис (`list[int]`, `X | None`), `Callable`, `Literal`, `TypedDict`, обобщения (`def f[T]`), чтение аннотаций (`__annotations__`, `get_type_hints`) и сужение типов через `isinstance`.

<!-- section: precise -->
## Подробности

**Аннотации хранятся, но не применяются.** У функции есть словарь `__annotations__`: имена параметров и ключ `"return"`. У класса и модуля тоже `__annotations__` для аннотированных переменных. Значения - обычные объекты (классы, `list[int]`...). История: аннотации функций - PEP 3107 (Python 3.0), семантика подсказок и модуль `typing` - PEP 484 (Python 3.5). В Python 3.14 аннотации вычисляются лениво (PEP 649/PEP 749), а с `from __future__ import annotations` они хранятся строками, поэтому для надёжного чтения используйте `typing.get_type_hints(obj)`: он превращает строки в объекты и заменяет `None` на `NoneType`.

**Современный синтаксис.**

* Встроенные обобщённые контейнеры: `list[int]`, `dict[str, int]`, `tuple[int, str]`, `tuple[int, ...]` (произвольной длины), `set[str]` (Python 3.9+).
* Объединение: `int | str` (Python 3.10+), раньше `Union[int, str]`; `X | None` равно `Optional[X]`.
* `Callable[[int, str], bool]` - функция от `int` и `str` к `bool` (`collections.abc.Callable` или `typing.Callable`).
* `Literal["r", "w"]` - только перечисленные значения; `Any` - «любой тип» (отключает проверку для значения).
* `TypedDict` - словарь с фиксированными строковыми ключами и типами значений: `class Movie(TypedDict): title: str; year: int`. Во время выполнения это обычный `dict`; описание лежит в `__annotations__`, `__required_keys__`, `__optional_keys__`. `total=False` делает ключи необязательными.

**Разбор аннотаций.** `typing.get_origin(tp)` возвращает «внешний» тип (`list` для `list[int]`, `Union`/`types.UnionType` для объединений, `Literal` для `Literal[...]`, `None` для обычного класса), `typing.get_args(tp)` - аргументы (`(int,)`). Они применяются, когда нужно написать собственную проверку или сериализатор.

**Обобщения.** Обобщённая функция сохраняет связь между типами: `def first[T](items: list[T]) -> T` означает «если на входе список строк, на выходе строка». Синтаксис параметров типа в заголовке функции и класса (`class Box[T]`) и инструкция `type Alias = ...` введены в **Python 3.12** (PEP 695). Параметры типа доступны в `__type_params__`. До этого писали `T = TypeVar("T")` и использовали `T` в аннотациях (для классов - `class Box(Generic[T])`); старый способ по-прежнему работает во всех поддерживаемых версиях. Объявление `[T]` само по себе ничего не проверяет во время выполнения.

**Сужение типов (narrowing).** Анализатор отслеживает проверки в коде: после `if isinstance(x, str):` внутри ветки `x` считается `str`, а в оставшейся части - тем, что осталось от объединения. Аналогично работают `x is None`, `callable(x)` и сравнения с `Literal`. Для вас, как для автора, это правило: пишите проверки так, чтобы они были понятны анализатору.

**Что даёт статическая проверка.** mypy или Pyright найдут `add("x", "y")` до запуска, проверят, что `X | None` обработан, и что возвращаемое значение соответствует аннотации. Аннотации не замедляют программу заметно, но и не ускоряют её. Параметры `-> None`, `Optional` и обобщения требуют аккуратности: аннотация должна отражать то, что код делает на самом деле.

<!-- section: examples -->
## Примеры

**Пример 1. Аннотации не проверяются, но сохраняются.**

```python run
def add(a: int, b: int) -> int:
    return a + b

print(add("x", "y"))
print(add.__annotations__)

class Config:
    host: str = "localhost"
    port: int

print(Config.__annotations__)
print(Config.host, hasattr(Config, "port"))
```
```output
xy
{'a': <class 'int'>, 'b': <class 'int'>, 'return': <class 'int'>}
{'host': <class 'str'>, 'port': <class 'int'>}
localhost False
```

**Пример 2. Современный синтаксис и разбор аннотаций.**

```python run
from collections.abc import Callable
from typing import Literal, TypedDict, get_args, get_origin, get_type_hints

def describe(name: str, tags: list[str] | None = None, mode: Literal["r", "w"] = "r") -> str:
    return name

hints = get_type_hints(describe)
print(hints["tags"])
print(get_args(hints["tags"]))
print(get_origin(list[int]), get_args(list[int]))
print(get_args(hints["mode"]))
print(get_args(Callable[[int, str], bool]))

class Movie(TypedDict):
    title: str
    year: int

m: Movie = {"title": "Solaris", "year": 1972}
print(type(m).__name__, Movie.__required_keys__ == {"title", "year"})
print(get_type_hints(Movie))
```
```output
list[str] | None
(list[str], <class 'NoneType'>)
<class 'list'> (<class 'int'>,)
('r', 'w')
([<class 'int'>, <class 'str'>], <class 'bool'>)
dict True
{'title': <class 'str'>, 'year': <class 'int'>}
```

**Пример 3. Обобщения PEP 695 (Python 3.12+) и устаревшие TypeVar.**

```python run
from typing import Generic, TypeVar

def first[T](items: list[T]) -> T:
    return items[0]

print(first([10, 20]), first(["a"]))
print(first.__type_params__)

class Box[T]:
    def __init__(self, item: T):
        self.item = item
    def get(self) -> T:
        return self.item

print(Box(3).get(), Box.__type_params__)

type IntList = list[int]
print(IntList.__value__)

U = TypeVar("U")

def first_legacy(items: list[U]) -> U:
    return items[0]

class LegacyBox(Generic[U]):
    pass

print(first_legacy([1]), LegacyBox.__parameters__)
```
```output
10 a
(T,)
3 (T,)
list[int]
1 (~U,)
```

**Пример 4. Сужение типов и проверка во время выполнения.**

```python run
from typing import get_type_hints

def size(x: int | str) -> int:
    if isinstance(x, str):
        return len(x)
    return x

print(size("hello"), size(7))

def checked(func):
    hints = get_type_hints(func)
    def wrapper(*args):
        names = list(hints)[:len(args)]
        for name, value in zip(names, args):
            expected = hints[name]
            if isinstance(expected, type) and not isinstance(value, expected):
                raise TypeError(f"{name}: ожидалось {expected.__name__}, получено {type(value).__name__}")
        return func(*args)
    return wrapper

@checked
def repeat(text: str, times: int) -> str:
    return text * times

print(repeat("ab", 2))
try:
    repeat("ab", "2")
except TypeError as exc:
    print("TypeError:", exc)
```
```output
5 7
abab
TypeError: times: ожидалось int, получено str
```

<!-- section: mistakes -->
## Типичные ошибки

* **Думать, что аннотация защищает от неверного типа.** Во время выполнения она ничего не проверяет; для проверки нужен анализатор (mypy, Pyright - в среде курса не установлены) либо собственный код.
* **`list` вместо `list[int]`** - не ошибка, но анализатор теряет информацию о содержимом.
* **Изменяемое значение по умолчанию** (`def f(x: list[int] = [])`) - аннотация не исправляет ловушку из [основ](lesson:python.fundamentals.functions): используйте `None`.
* **Путаница `Optional[int]` с «необязательным параметром».** `Optional[int]` - это `int | None`, а необязательность задаёт значение по умолчанию.
* **Использование `X | Y` на старых версиях.** До Python 3.10 это ошибка; синтаксис `def f[T]` и `type Alias = ...` требует Python 3.12+.
* **Чтение `__annotations__` напрямую при строковых аннотациях.** С `from __future__ import annotations` значения - строки; используйте `get_type_hints`.
* **`isinstance(x, list[int])`** вызывает `TypeError`: параметризованные обобщённые типы нельзя использовать в `isinstance`; проверяйте `isinstance(x, list)` и затем элементы.
* **Лишний `Any`.** Он отключает проверку и «заражает» код вокруг.
* **Забыть, что `bool` является подклассом `int`:** `isinstance(True, int)` равно `True`, и простая runtime-проверка принимает `True` как `int`.

<!-- section: summary -->
## Итог

* Аннотации типов хранятся в `__annotations__`, но интерпретатор их не проверяет; проверку делают mypy или Pyright (их интеграция планируется, сейчас не установлены).
* Синтаксис: `list[int]`, `dict[str, int]`, `X | None` (3.10+), `Callable[[A], B]`, `Literal[...]`, `TypedDict`.
* Обобщения PEP 695: `def f[T](x: T) -> T`, `class Box[T]`, `type A = ...` - Python 3.12+; для старых версий - `TypeVar`/`Generic`.
* `get_type_hints`, `get_origin`, `get_args` позволяют разбирать аннотации и писать собственные проверки во время выполнения.
* `isinstance` сужает тип в ветках кода для анализатора.
* Вы закончили курс: теперь можно переходить к разработке прикладных программ или повторить [основы классов](lesson:python.advanced.classes-and-objects).
