<!-- section: simple -->
## Зачем нужен collections

Встроенных `list`, `dict`, `set` и `tuple` хватает на большинство задач, но у каждого есть «неудобные» сценарии: посчитать, сколько раз встретилось каждое слово; сгруппировать записи по ключу, не проверяя на каждом шаге «а есть ли уже такой ключ»; быстро забирать элементы из начала очереди; хранить запись с именованными полями. Для них в модуле `collections` есть готовые контейнеры, которые короче в записи, понятнее в чтении и в ряде случаев асимптотически быстрее.

* `Counter` - словарь «элемент -> сколько раз встретился».
* `defaultdict` - словарь, который сам создаёт значение для отсутствующего ключа.
* `deque` - двусторонняя очередь: быстро с обоих концов, при желании с ограниченной длиной.
* `namedtuple` - кортеж, у элементов которого есть имена.
* `ChainMap` - несколько словарей, видимых как один (слои настроек).

Основы словарей и списков вы уже знаете из уроков [«Словари и множества»](lesson:python.fundamentals.dicts-and-sets) и [«Списки»](lesson:python.fundamentals.lists). Здесь мы разбираем, что выбрать вместо них и сколько это стоит.

<!-- section: precise -->
## Назначение, API и стоимость

**Counter.** Подкласс `dict`, значения - целые счётчики. `Counter(iterable)` считает элементы; `Counter(a=2)` и `Counter({'a': 2})` тоже допустимы. Отсутствующий ключ даёт `0` и **не** вставляется. Основные операции:

* `most_common(n)` - список пар `(элемент, счётчик)` по убыванию счётчика; без `n` - все пары. Элементы с равным счётчиком идут в порядке первого появления (сортировка устойчива, словарь помнит порядок вставки). Для малого `n` реализация использует `heapq.nlargest`, поэтому полная сортировка не требуется.
* `update(iterable_or_mapping)` **прибавляет** (в отличие от `dict.update`, которая заменяет), `subtract(...)` вычитает и может дать нули и отрицательные значения.
* Арифметика `+`, `-`, `&`, `|` между счётчиками **отбрасывает** значения `<= 0`. `total()` (Python 3.10+) возвращает сумму счётчиков, `elements()` повторяет каждый элемент столько раз, сколько он встретился.

**defaultdict.** `defaultdict(factory)`, где `factory` - вызываемый объект без аргументов (`list`, `int`, `set`, `lambda: ...`). Только обращение `d[key]` (метод `__missing__`) вызывает фабрику и **сохраняет** значение; `d.get(key)` и `key in d` словарь не меняют. Типичные фабрики: `list` для группировки, `int` для счётчиков, `set` для уникальных значений. Если фабрика `None`, поведение как у обычного `dict` (`KeyError`).

**deque.** Двусторонняя очередь: `append`, `appendleft`, `pop`, `popleft` - O(1); `rotate(n)` - O(n); индексирование `d[i]` в середине стоит O(n), срезов у deque нет. Конструктор `deque(iterable, maxlen=N)` создаёт **ограниченную** очередь: при переполнении элемент с противоположного конца отбрасывается, поэтому `deque(maxlen=N)` - готовое «окно последних N». `maxlen=None` - без ограничений. В отличие от этого, `list.pop(0)` и `list.insert(0, x)` сдвигают все элементы и стоят O(n), что превращает цикл «забрать первый элемент» в O(n²).

**namedtuple.** `namedtuple("Point", ["x", "y"])` создаёт подкласс `tuple`: поля доступны по имени и по индексу, экземпляр неизменяем, его можно распаковывать и использовать как ключ словаря. Полезные члены: `_asdict()`, `_replace(**changes)` (возвращает **новый** экземпляр), `_fields`. Параметр `defaults=` задаёт значения по умолчанию для крайних правых полей. Если нужны методы, изменяемость и проверка типов - берите [`dataclass`](lesson:python.advanced.dunder-methods-and-dataclasses).

**ChainMap.** `ChainMap(a, b, c)` ищет ключ по очереди в `a`, `b`, `c`; запись и удаление идут только в **первый** словарь. Не копирует данные: изменения базовых словарей видны сразу. Типичное применение - слои настроек «аргументы командной строки > переменные окружения > значения по умолчанию»; метод `new_child()` добавляет новый слой сверху.

**OrderedDict.** С Python 3.7 обычный `dict` помнит порядок вставки, поэтому `OrderedDict` нужен редко: у него есть `move_to_end(key)` и `popitem(last=False)`, удобные для LRU-кэшей, а сравнение двух `OrderedDict` учитывает порядок.

**Ограничения.** `Counter` и `defaultdict` требуют хешируемых ключей. Арифметические операторы `Counter` предполагают положительные счётчики. Отдельные `append`/`pop` у `deque` атомарны, но составные действия «проверил и забрал» - нет, для потоков нужна своя синхронизация.

<!-- section: examples -->
## Примеры

**Пример 1. Counter: подсчёт, порядок при равенстве, арифметика.**

```python run
from collections import Counter

c = Counter("abracadabra")
print(c.most_common(2))
print(c["z"], "z" in c)
print(Counter("bbaacc").most_common())

c.update("aaz")
c.subtract("bb")
print(c["a"], c["b"], c["z"])
print(Counter(a=2, b=1) - Counter(a=1, b=5))
print(sorted(Counter(a=2, b=1).elements()), Counter(a=2, b=1).total())
```
```output
[('a', 5), ('b', 2)]
0 False
[('b', 2), ('a', 2), ('c', 2)]
7 0 1
Counter({'a': 1})
['a', 'a', 'b'] 3
```

**Пример 2. defaultdict и опасное чтение.**

```python run
from collections import defaultdict

by_len = defaultdict(list)
for word in ["to", "be", "or", "not", "to", "be"]:
    by_len[len(word)].append(word)
print(dict(by_len))

d = defaultdict(int)
print(d.get("x"), "x" in d, len(d))
if d["x"] == 0:       # чтение через [] вставило ключ
    pass
print("x" in d, len(d))

plain = dict(by_len)  # возвращайте наружу обычный dict
try:
    plain[99]
except KeyError as exc:
    print("KeyError", exc)
```
```output
{2: ['to', 'be', 'or', 'to', 'be'], 3: ['not']}
None False 0
True 1
KeyError 99
```

**Пример 3. deque: очередь и окно последних N.**

```python run
from collections import deque

q = deque([1, 2, 3])
q.appendleft(0)
q.append(4)
print(q)
print(q.popleft(), q.pop(), q)

window = deque(maxlen=3)
for x in range(6):
    window.append(x)
print(window, window.maxlen)

r = deque("abcde")
r.rotate(2)
print("".join(r))
```
```output
deque([0, 1, 2, 3, 4])
0 4 deque([1, 2, 3])
deque([3, 4, 5], maxlen=3) 3
deabc
```

**Пример 4. namedtuple и ChainMap.**

```python run
from collections import ChainMap, namedtuple

Point = namedtuple("Point", ["x", "y"], defaults=[0])
p = Point(3)
print(p, p.x, p[1], tuple(p), p._asdict())
p2 = p._replace(y=9)
print(p, p2, p == (3, 0), Point._fields)
x, y = p2
print(x + y)

defaults = {"color": "red", "size": 10}
user = {"size": 12}
cfg = ChainMap(user, defaults)
print(cfg["color"], cfg["size"], list(cfg))
cfg["font"] = "mono"   # запись идёт в первый словарь
print(user, "font" in defaults)
```
```output
Point(x=3, y=0) 3 0 (3, 0) {'x': 3, 'y': 0}
Point(x=3, y=0) Point(x=3, y=9) True ('x', 'y')
12
red 12 ['color', 'size']
{'size': 12, 'font': 'mono'} False
```

<!-- section: mistakes -->
## Типичные ошибки

* **Чтение через `[]` у `defaultdict`.** `if d[key]:` вставляет ключ. Для проверки используйте `key in d` или `d.get(key)`; наружу из функции возвращайте `dict(d)`.
* **`Counter.update` как замена.** `c.update({"a": 5})` прибавляет 5, а не записывает 5. Для записи используйте `c["a"] = 5`.
* **Ожидание отрицательных значений после `-`.** `Counter(a=1) - Counter(a=3)` пуст: операторы отбрасывают значения `<= 0`; нужен `subtract`.
* **Опора на неявный порядок при равных счётчиках.** Он определён (порядок первого появления), но читается плохо: если порядок важен пользователю, задайте его явно через `sorted(..., key=...)`.
* **`list.pop(0)` в цикле.** Для очереди это O(n) на каждый шаг; используйте `deque.popleft()`.
* **Доступ по индексу в больших `deque`.** `d[len(d) // 2]` - O(n); если нужен произвольный доступ, берите `list`.
* **Изменение `namedtuple`.** `p.x = 1` даёт `AttributeError`; используйте `_replace` или `dataclass`.
* **Значение вместо фабрики.** `defaultdict([])` - `TypeError`: нужна вызываемая фабрика (`list`), иначе все ключи делили бы один объект.
* **Ожидание копии от `ChainMap`.** Изменение базового словаря сразу видно через `ChainMap`; запись и `del` затрагивают только первый слой.

<!-- section: summary -->
## Итог

* `Counter` считает; `most_common` при равенстве сохраняет порядок первого появления; `update` прибавляет, арифметика отбрасывает неположительные значения.
* `defaultdict(list/int/set)` убирает проверки «ключ уже есть», но чтение `d[key]` вставляет ключ; наружу отдавайте `dict`.
* `deque` - O(1) с обоих концов, `maxlen` даёт окно последних N; середина - O(n); `list.pop(0)` - O(n).
* `namedtuple` - неизменяемый кортеж с именами; `_replace` создаёт копию; для изменяемых записей с методами нужен dataclass.
* `ChainMap` - слои словарей без копирования; запись только в первый слой.
* Дальше: [кучи и бинарный поиск](lesson:python.stdlib.heapq-and-bisect) - структуры для «наименьший/наибольший элемент» и упорядоченных списков.
