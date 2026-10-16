<!-- section: simple -->
## Инструменты, которые не нужно писать самому

В уроке [«Итераторы и генераторы»](lesson:python.advanced.iterators-and-generators) вы познакомились с `islice`, `chain`, `groupby`, `accumulate` и `product`. Этот урок расширяет набор тремя модулями.

* **`itertools`** - ленивые итераторы для перебора комбинаций (`permutations`, `combinations`, `product`), скользящих окон (`pairwise`), разбиения на блоки (`batched`) и прочих «деталей конвейера».
* **`functools`** - инструменты для работы с функциями: свёртка `reduce`, кэш `cache`/`lru_cache`, диспетчеризация по типу `singledispatch`, адаптер старого компаратора `cmp_to_key`, фиксация аргументов `partial`.
* **`operator`** - функции вместо операторов: `itemgetter`, `attrgetter`, `methodcaller`, `add`, `mul`. Ими удобно заменять простые `lambda` в `key=` и `map`.

Главный вопрос при выборе: что из этого *читается* лучше цикла, а что только выглядит хитро. Готовый инструмент выигрывает, когда он делает ровно то, что вы собирались написать, и не сложнее.

Версии: `itertools.pairwise` - Python 3.10+, `functools.cache` - 3.9+, `itertools.batched` - **3.12+**. Курс рассчитан на Python 3.12 и новее.

<!-- section: precise -->
## API, стоимость и ограничения

**Комбинаторика (`itertools`).** Все функции возвращают ленивые итераторы, но размер результата растёт очень быстро.

* `permutations(it, r=None)` - упорядоченные выборки без повторов; их `n!/(n-r)!` штук (`math.perm(n, r)`). Порядок зависит от позиций элементов, а не от их значений.
* `combinations(it, r)` - неупорядоченные выборки без повторов; `n!/(r!(n-r)!)` штук (`math.comb(n, r)`), порядок лексикографический по позициям. `combinations_with_replacement` допускает повторы.
* `product(*its, repeat=1)` - декартово произведение; размер - произведение длин.
* `pairwise(it)` - перекрывающиеся соседние пары `(a, b), (b, c), ...`; для менее чем двух элементов пусто.
* `batched(it, n)` - непересекающиеся кортежи по `n` элементов, последний может быть короче; `n < 1` - `ValueError`; в 3.13+ есть `strict=True`, требующий полных блоков.
* Прочее: `zip_longest`, `starmap`, `takewhile`/`dropwhile`, `compress`, `cycle`, `repeat`, `tee` (буферизует данные).

Пока потребитель не просит значения, итератор ничего не вычисляет, однако `list(permutations(range(10)))` создаст 3 628 800 кортежей. Оценивайте размер заранее.

**`functools.reduce(func, iterable, initial)`** последовательно сворачивает элементы бинарной функцией. Без `initial` на пустой последовательности будет `TypeError`. Для сумм, минимумов и максимумов есть `sum`, `min`, `max`; для произведения - `math.prod`. `reduce` оправдан для композиции функций и нестандартных операций.

**Кэширование.** `@lru_cache(maxsize=128)` хранит последние результаты; `@cache` (3.9+) - то же самое без ограничения (`lru_cache(maxsize=None)`). Ключ строится из аргументов, поэтому они должны быть **хешируемыми** (`list` и `dict` дадут `TypeError`). Кэш подходит только для **чистых** функций (результат зависит лишь от аргументов). Методы `cache_info()` и `cache_clear()` показывают статистику и очищают кэш. Риски: неограниченный рост памяти у `cache`; кэш хранит ссылки на аргументы и результаты, поэтому кэширование метода удерживает `self`; изменяемый результат делится между всеми вызовами.

**`singledispatch`.** Функция выбирает реализацию по типу **первого** аргумента: базовая реализация - по умолчанию, остальные регистрируются `@func.register(тип)` (или по аннотации первого параметра). Учитывается наследование: для `bool` без своей реализации сработает реализация для `int`. Для методов есть `singledispatchmethod`.

**`cmp_to_key(cmp)`** превращает старый компаратор `cmp(a, b)` (отрицательное, ноль, положительное) в `key`. Он медленнее обычного ключа и нужен, когда порядок задаётся попарным сравнением. Для нескольких полей используйте `key=itemgetter(a, b)` или кортеж.

**`operator`.** `itemgetter(k)` берёт `obj[k]`, `itemgetter(a, b)` возвращает кортеж значений; `attrgetter("x.y")` берёт атрибут, в том числе вложенный; `methodcaller("upper")` вызывает метод. Эти объекты работают быстрее эквивалентного `lambda`, и их легко комбинировать с `sorted`, `min`, `max`, `groupby`. Сортировка в Python **устойчива**: равные по ключу элементы сохраняют входной порядок, поэтому можно сортировать последовательно по разным ключам.

**`partial(func, *args, **kw)`** фиксирует часть аргументов, возвращая новый вызываемый объект.

<!-- section: examples -->
## Примеры

**Пример 1. Комбинаторика и размер заранее.**

```python run
import itertools
import math

print(list(itertools.permutations("abc", 2)))
print(list(itertools.combinations("abcd", 2)))
print(list(itertools.product("ab", repeat=2)))
print(math.perm(10, 3), math.comb(10, 3), math.factorial(10))

print(list(itertools.pairwise([1, 4, 9, 16])))
print(list(itertools.batched("abcdefg", 3)))      # Python 3.12+
try:
    list(itertools.batched("abc", 0))
except ValueError:
    print("ValueError: n должно быть не меньше 1")
```
```output
[('a', 'b'), ('a', 'c'), ('b', 'a'), ('b', 'c'), ('c', 'a'), ('c', 'b')]
[('a', 'b'), ('a', 'c'), ('a', 'd'), ('b', 'c'), ('b', 'd'), ('c', 'd')]
[('a', 'a'), ('a', 'b'), ('b', 'a'), ('b', 'b')]
720 120 3628800
[(1, 4), (4, 9), (9, 16)]
[('a', 'b', 'c'), ('d', 'e', 'f'), ('g',)]
ValueError: n должно быть не меньше 1
```

**Пример 2. reduce, partial и operator.**

```python run
import operator
from functools import partial, reduce

print(reduce(operator.mul, [1, 2, 3, 4], 1), reduce(max, [3, 9, 2]))
try:
    reduce(operator.add, [])
except TypeError as exc:
    print("TypeError:", exc)

pow2 = partial(pow, 2)
print(pow2(10))

rows = [{"name": "bob", "age": 30}, {"name": "ann", "age": 25}, {"name": "cid", "age": 30}]
print([r["name"] for r in sorted(rows, key=operator.itemgetter("age", "name"))])

class P:
    def __init__(self, x, y):
        self.pos = (x, y)

pts = [P(3, 1), P(1, 2)]
print(sorted(pts, key=operator.attrgetter("pos"))[0].pos)
print(operator.methodcaller("upper")("abc"))
```
```output
24 9
TypeError: reduce() of empty iterable with no initial value
1024
['ann', 'bob', 'cid']
(1, 2)
ABC
```

**Пример 3. cache, lru_cache и ловушка нехешируемых аргументов.**

```python run
from functools import cache, lru_cache

calls = 0

@cache
def square(n):
    global calls
    calls += 1
    return n * n

print(square(4), square(4), square(5), calls)
print(square.cache_info())

@lru_cache(maxsize=2)
def tag(x):
    return x

for x in "abca":
    tag(x)
print(tag.cache_info())

try:
    square([1, 2])
except TypeError as exc:
    print("TypeError:", exc)
square.cache_clear()
print(square.cache_info())
```
```output
16 16 25 2
CacheInfo(hits=1, misses=2, maxsize=None, currsize=2)
CacheInfo(hits=0, misses=4, maxsize=2, currsize=2)
TypeError: unhashable type: 'list'
CacheInfo(hits=0, misses=0, maxsize=None, currsize=0)
```

**Пример 4. singledispatch и cmp_to_key.**

```python run
from functools import cmp_to_key, singledispatch

@singledispatch
def show(x):
    return f"object {x!r}"

@show.register(int)
def _(x):
    return f"int {x}"

@show.register(list)
def _(x):
    return "list of " + str(len(x))

print(show(3), show(True), show([1, 2]), show("s"))

def by_concat(a, b):
    return -1 if a + b > b + a else (1 if a + b < b + a else 0)

nums = ["3", "30", "34", "5", "9"]
print("".join(sorted(nums, key=cmp_to_key(by_concat))))
print(sorted(nums))   # обычная сортировка строк даёт другой порядок
```
```output
int 3 int True list of 2 object 's'
9534330
['3', '30', '34', '5', '9']
```

<!-- section: mistakes -->
## Типичные ошибки

* **Перебор без оценки размера.** `list(permutations(range(12)))` - около полумиллиарда кортежей. Сначала `math.perm`/`math.comb`.
* **Путать `pairwise` и `batched`.** Первое - скользящее окно с перекрытием, второе - непересекающиеся блоки.
* **`batched` на старом Python.** До 3.12 функции нет (`AttributeError`). Проверяйте версию интерпретатора.
* **`reduce` без `initial` на пустом входе.** Получите `TypeError`; задавайте начальное значение.
* **Кэш для нечистых функций.** Функции, зависящие от времени, файла или глобального состояния, будут возвращать устаревшие результаты.
* **Кэш с нехешируемыми аргументами.** Списки и словари дают `TypeError`; конвертируйте в `tuple`/`frozenset`.
* **`@cache` на методе.** Кэш удерживает `self`, объекты не освобождаются; для методов задавайте `maxsize` или кэшируйте на уровне функций.
* **Бесконечный рост `@cache`.** Для потока разных аргументов нужен `lru_cache(maxsize=N)`.
* **Изменяемый результат кэша.** Вызывающий код может изменить закэшированный список; возвращайте неизменяемые структуры.
* **Ожидание диспетчеризации по второму аргументу.** `singledispatch` смотрит только на первый.
* **`cmp_to_key` там, где хватает `key`.** Лишняя медленность и больше места для ошибок в компараторе (возврат `bool` вместо числа).
* **`itemgetter` с одним ключом и с несколькими.** `itemgetter("a")` возвращает значение, `itemgetter("a", "b")` - кортеж; тип результата зависит от количества аргументов.

<!-- section: summary -->
## Итог

* `permutations`, `combinations`, `product` ленивы, но их размер взрывной; считайте `math.perm`/`math.comb` заранее. `pairwise` (3.10+) - окно из двух, `batched` (3.12+) - блоки.
* `reduce` нужен для нестандартной свёртки и композиции; для сумм и экстремумов есть встроенные функции. Всегда задавайте `initial`, если вход может быть пустым.
* `cache`/`lru_cache` - только для чистых функций с хешируемыми аргументами; следите за памятью и за `self` в методах.
* `singledispatch` выбирает реализацию по типу первого аргумента, учитывает наследование.
* `itemgetter`/`attrgetter` заменяют простые `lambda` в `key=`; сортировка устойчива. `cmp_to_key` - запасной вариант для попарных сравнений.
* Дальше: [pathlib, os, shutil, tempfile](lesson:python.stdlib.pathlib-os-shutil-tempfile) - безопасная работа с файловой системой.
