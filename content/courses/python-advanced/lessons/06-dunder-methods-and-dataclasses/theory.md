<!-- section: simple -->
## Как научить свой класс вести себя как встроенный тип

Когда вы пишете `len(x)`, `a + b`, `x in items`, `print(obj)` или `sorted(objs)`, Python за кулисами вызывает **специальные методы** с двойными подчёркиваниями (dunder - от double underscore): `__len__`, `__add__`, `__contains__`, `__str__`, `__lt__`. Реализуя их в своём классе, вы вписываете его в язык: объекты можно сравнивать, печатать, класть в множества и обходить в цикле.

Самые полезные группы:

* **Представление**: `__repr__` (для разработчика, однозначное, видно в отладчике и логах) и `__str__` (для человека, используется в `print`).
* **Равенство и хеш**: `__eq__` отвечает на `a == b`, `__hash__` нужен, чтобы объект можно было положить в `set` или использовать ключом `dict`. Они связаны правилом: *равные объекты обязаны иметь равные хеши*.
* **Порядок**: `__lt__` и компания нужны для `sorted`, `min`, `max`.
* **Контейнер**: `__len__`, `__getitem__`, `__contains__` - чтобы объект работал как коллекция.

Писать всё это руками утомительно, особенно для простых классов-«записей». Для них придумали `dataclasses`: декоратор по аннотациям полей сам генерирует `__init__`, `__repr__`, `__eq__` и по желанию сравнения, неизменяемость и многое другое. Декоратор - частный случай [декораторов классов](lesson:python.advanced.decorators). Основы классов см. в [предыдущем уроке](lesson:python.advanced.classes-and-objects).

<!-- section: precise -->
## Подробности

**`__repr__` и `__str__`.** `repr(x)` вызывает `x.__repr__()`, `str(x)` - `x.__str__()`. Если `__str__` не определён, `str` откатывается к `__repr__`; наоборот - нет. Контейнеры (`list`, `dict`) показывают элементы через `repr`. Хороший `__repr__` выглядит как вызов конструктора: `Money(150, 'USD')`. Формат `f"{x!r}"` явно запрашивает repr.

**`__eq__`.** Должен вернуть `True`/`False` или `NotImplemented`, если не умеет сравнивать с объектом такого типа: тогда Python попробует отражённую операцию у второго операнда, а затем сравнит по тождеству (для `==` результатом будет `False`, для порядка - `TypeError`). Не возбуждайте исключение на чужом типе. `__ne__` по умолчанию выводится из `__eq__`.

**Контракт `__eq__`/`__hash__`.** Если `a == b`, то `hash(a) == hash(b)`. Обратное неверно (коллизии допустимы). Правила Python: если класс определяет `__eq__`, а `__hash__` нет, то `__hash__` неявно становится `None`, и экземпляры *нехешируемы* (`TypeError: unhashable type`). Хеш должен считаться по тем же полям, что и равенство, и поля должны быть неизменяемыми - иначе объект «потеряется» в множестве после изменения.

**Упорядочивание.** Для `<`, `<=`, `>`, `>=` нужны методы `__lt__`, `__le__`, `__gt__`, `__ge__`. Декоратор `functools.total_ordering` по одному из них (плюс `__eq__`) достраивает остальные. `sorted` и `min`/`max` используют только `<`. Для разных типов возвращайте `NotImplemented`.

**Контейнеры.** `len(x)` вызывает `__len__`; пустой по длине объект при `bool(x)` считается ложным, если нет `__bool__`. `x[i]` вызывает `__getitem__(i)`; для среза `x[a:b]` приходит объект `slice`. `i in x` вызывает `__contains__`, а если его нет - перебирает элементы. Итерация `for` без `__iter__` работает через `__getitem__` с индексами 0, 1, 2... до `IndexError`, но надёжнее определить `__iter__` (см. [итераторы](lesson:python.advanced.iterators-and-generators)).

**dataclasses.** `@dataclass` читает аннотации класса и генерирует методы. Параметры (основные):

* `eq=True` (по умолчанию) - `__eq__` сравнивает кортежи полей; при `eq=True` и `frozen=False` `__hash__` ставится в `None`.
* `frozen=True` - присваивание полей запрещено (`FrozenInstanceError`); вместе с `eq=True` генерируется `__hash__` по полям.
* `order=True` - генерируются `<`, `<=`, `>`, `>=` как сравнение кортежей полей в порядке объявления.
* `slots=True` (Python 3.10+) - класс получает `__slots__`, экземпляры без `__dict__`.
* Поля: `x: int = 0` (значение по умолчанию), `field(default_factory=list)` для изменяемых значений (запись `x: list = []` приводит к `ValueError`), `field(repr=False)`, `field(compare=False)`.
* Поля без значения по умолчанию не могут идти после полей со значением. Метод `__post_init__` вызывается после `__init__` для проверок.

Dataclass остаётся обычным классом - в него можно добавлять методы.

<!-- section: examples -->
## Примеры

**Пример 1. repr, str и контейнеры.**

```python run
class Money:
    def __init__(self, cents):
        self.cents = cents
    def __repr__(self):
        return f"Money({self.cents!r})"
    def __str__(self):
        return f"{self.cents / 100:.2f}"

m = Money(250)
print(m)
print(repr(m))
print([m, Money(5)])
print(f"{m!r} / {m}")

class Plain:
    pass
print(repr(Plain())[:15])
```
```output
2.50
Money(250)
[Money(250), Money(5)]
Money(250) / 2.50
<__main__.Plain
```

**Пример 2. Контракт eq и hash.** Формулировка сообщения зависит от версии Python (в 3.14 к `unhashable type: 'OnlyEq'` добавлено начало «cannot use 'OnlyEq' as a set element»), поэтому пример проверяет тип исключения и фразу `unhashable type`, а не весь текст.

```python run
class OnlyEq:
    def __init__(self, v):
        self.v = v
    def __eq__(self, other):
        return isinstance(other, OnlyEq) and self.v == other.v

print(OnlyEq.__hash__)
try:
    {OnlyEq(1)}
except TypeError as exc:
    print(type(exc).__name__, "unhashable type" in str(exc))

class Both(OnlyEq):
    def __hash__(self):
        return hash(self.v)

print(len({Both(1), Both(1), Both(2)}))
print(OnlyEq(1) == 1)
```
```output
None
TypeError True
2
False
```

**Пример 3. Упорядочивание и протокол контейнера.**

```python run
from functools import total_ordering

@total_ordering
class Version:
    def __init__(self, *parts):
        self.parts = parts
    def __eq__(self, other):
        return self.parts == other.parts
    def __lt__(self, other):
        return self.parts < other.parts
    def __repr__(self):
        return "V" + ".".join(map(str, self.parts))

print(sorted([Version(1, 10), Version(1, 2)]), Version(1, 2) >= Version(1, 2))

class Deck:
    def __init__(self, cards):
        self._cards = list(cards)
    def __len__(self):
        return len(self._cards)
    def __getitem__(self, i):
        return self._cards[i]
    def __contains__(self, card):
        return card in self._cards

d = Deck(["A", "K", "Q"])
print(len(d), d[0], d[-1], d[1:], "K" in d)
print([c for c in d], bool(Deck([])))
```
```output
[V1.2, V1.10] True
3 A Q ['K', 'Q'] True
['A', 'K', 'Q'] False
```

**Пример 4. dataclass: поля, default_factory, frozen, order, slots.**

```python run
from dataclasses import FrozenInstanceError, dataclass, field

@dataclass
class Task:
    title: str
    tags: list[str] = field(default_factory=list)
    done: bool = False

a, b = Task("one"), Task("two")
a.tags.append("urgent")
print(a, b.tags)
print(Task("x") == Task("x"), Task("x") is Task("x"))

try:
    @dataclass
    class Bad:
        items: list = []
except ValueError:
    print("ValueError: изменяемое значение по умолчанию")

@dataclass(frozen=True, order=True)
class Point:
    x: int
    y: int = 0

print(Point(1, 2) < Point(1, 3), len({Point(1), Point(1, 0)}))
try:
    Point(1).x = 5
except FrozenInstanceError:
    print("FrozenInstanceError")

@dataclass(slots=True)
class Tiny:
    n: int

print(hasattr(Tiny(1), "__dict__"))
print(Task.__hash__)
```
```output
Task(title='one', tags=['urgent'], done=False) []
True False
ValueError: изменяемое значение по умолчанию
True 1
FrozenInstanceError
False
None
```

<!-- section: mistakes -->
## Типичные ошибки

* **`__eq__` без `__hash__`.** Экземпляры нельзя класть в `set`/`dict`. Определите `__hash__` по тем же полям (или оставьте объект нехешируемым осознанно).
* **Хеш изменяемого объекта.** Если поле, входящее в хеш, меняется, объект «теряется» в словаре. Хешируйте только неизменяемое состояние, для таких классов используйте `frozen=True`.
* **`__eq__`, который падает на чужом типе** (`other.v` без проверки). Проверяйте тип и возвращайте `NotImplemented`.
* **Только `__str__` без `__repr__`.** В списках и отладчике вы увидите `<... object at 0x...>`.
* **Изменяемое значение по умолчанию в dataclass** (`items: list = []`) запрещено - используйте `field(default_factory=list)`.
* **Поле без значения после поля со значением** в dataclass - `TypeError` при создании класса.
* **`order=True` и несовместимые типы.** Сравнение кортежей полей может дать `TypeError`, если поля разных типов.
* **Путаница `frozen` с глубокой неизменяемостью.** `frozen=True` запрещает присваивание полей, но список внутри поля по-прежнему можно изменить.
* **`slots=True` на Python ниже 3.10** недоступен.

<!-- section: summary -->
## Итог

* `__repr__` - для разработчика (определяйте всегда), `__str__` - для пользователя.
* `a == b` влечёт `hash(a) == hash(b)`; класс только с `__eq__` нехешируем.
* `total_ordering` достраивает сравнения по `__eq__` и одному из `__lt__`/`__le__`/`__gt__`/`__ge__`.
* Контейнер: `__len__`, `__getitem__` (индексы и `slice`), `__contains__`, итерация.
* `@dataclass` генерирует `__init__`, `__repr__`, `__eq__`; параметры `frozen`, `order`, `slots` (3.10+); изменяемые значения - через `default_factory`.
* Дальше: [наследование, композиция и протоколы](lesson:python.advanced.inheritance-composition-protocols) и [аннотации типов](lesson:python.advanced.typing-fundamentals).
