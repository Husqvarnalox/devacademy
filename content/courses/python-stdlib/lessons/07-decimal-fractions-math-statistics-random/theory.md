<!-- section: simple -->
## Числа бывают разные

В уроке [«Числа и операторы»](lesson:python.fundamentals.numbers-and-operators) вы работали с `int` и `float`. Для большинства задач их достаточно, но есть четыре ситуации, где нужны другие инструменты.

* **Деньги и десятичные величины.** `float` хранит числа в двоичном виде, и `0.1 + 0.2` не равно `0.3`. Для денег используйте `decimal.Decimal`.
* **Точные дроби.** `fractions.Fraction(1, 3)` хранит ровно одну треть, без потери точности.
* **Сравнение и суммирование `float`.** `math.isclose` сравнивает с допуском, `math.fsum` суммирует аккуратнее обычного `sum`.
* **Статистика и случайность.** `statistics` считает среднее, медиану и разброс; `random` даёт воспроизводимые псевдослучайные числа для моделирования, а `secrets` - непредсказуемые значения для паролей и токенов.

Главное правило: **float - для измерений, Decimal - для денег, Fraction - для точной алгебры, secrets - для секретов, random - для всего остального случайного**.

<!-- section: precise -->
## Точность, округление и контексты

**float.** Это двойная точность IEEE 754: 53 бита мантиссы, около 15-17 значащих десятичных цифр. Число 0.1 хранится приближённо. Сравнивать `float` через `==` можно только когда значения получены без вычислений; иначе применяйте `math.isclose(a, b, rel_tol=1e-09, abs_tol=0.0)`. При сравнении с нулём нужен ненулевой `abs_tol`. `math.fsum(it)` возвращает точно округлённую сумму `float` и не накапливает ошибку. В Python 3.12+ встроенный `sum` для `float` тоже использует компенсированное суммирование, но `fsum` остаётся строгой гарантией и не зависит от версии.

**Decimal.**

* Создавайте из **строки** (`Decimal("0.1")`) или из `int`. `Decimal(0.1)` воспроизводит двоичную ошибку `float` целиком.
* Арифметика `+ - * /` округляет результат до точности контекста (по умолчанию 28 значащих цифр). Контекст задаётся через `decimal.getcontext()` или локально через `decimal.localcontext()`.
* `quantize(Decimal("0.01"), rounding=...)` приводит число к заданному показателю. Режимы: `ROUND_HALF_EVEN` (по умолчанию в контексте: половина идёт к чётной цифре, «банковское округление»), `ROUND_HALF_UP` (половина уходит от нуля), `ROUND_DOWN`, `ROUND_UP`, `ROUND_FLOOR`, `ROUND_CEILING`.
* Смешивать `Decimal` и `float` в арифметике нельзя (`TypeError`), сравнивать можно. `Decimal` и `int` смешиваются свободно.
* Некорректная строка даёт `decimal.InvalidOperation`; он **не** является подклассом `ValueError`. `Decimal("NaN")` и `Decimal("Infinity")` допустимы, проверяйте `is_finite()`.
* Операции в Python-реализации заметно медленнее `float` (в CPython используется библиотека libmpdec на C, но всё равно в разы медленнее).

**Деньги: правила.** Храните суммы как `Decimal` из строк (или целые копейки). Не округляйте промежуточные значения, округляйте один раз в конце. Делите сумму через целые копейки (`divmod`), чтобы части в сумме давали исходную сумму.

**Fraction.** `Fraction("1/3")`, `Fraction("0.1")`, `Fraction(3, 6) == Fraction(1, 2)`. Результат всегда несократимая дробь. Подходит для точных вычислений небольшого размера; числитель и знаменатель растут, поэтому длинные цепочки операций становятся медленными. Из `float` создаётся точное двоичное значение, поэтому `Fraction(0.1)` не равно `1/10`; для приближения служит `limit_denominator`.

**statistics.** `mean`, `fmean` (быстрое, всегда `float`), `median`, `mode`, `multimode`, `quantiles`, `variance`/`stdev` (выборочные, делят на n - 1, нужно минимум два значения), `pvariance`/`pstdev` (генеральные, делят на n). Пустые данные и слишком короткие выборки дают `statistics.StatisticsError`, который является подклассом `ValueError`.

**random.** `random.Random(seed)` создаёт отдельный генератор (Mersenne Twister). `seed` определяет всю последовательность. Методы: `random()`, `randint(a, b)` (оба конца включены), `choice`, `choices` (с возвратом), `sample` (без возврата), `shuffle` (на месте). Глобальные функции `random.*` используют общий скрытый генератор, поэтому для воспроизводимых экспериментов создавайте собственный `Random(seed)`. Последовательность `random()` при одном seed стабильна между версиями, а у `shuffle`, `choice`, `sample` алгоритмы документацией не гарантируются.

**secrets.** `token_hex(n)`, `token_urlsafe(n)`, `token_bytes(n)`, `choice`, `randbelow(n)`, `compare_digest(a, b)` (сравнение за постоянное время). Источник - ОС, значения непредсказуемы и не воспроизводятся.

<!-- section: examples -->
## Примеры

**Пример 1. Ошибка float и Decimal из строки.**

```python run
from decimal import Decimal

print(0.1 + 0.2, 0.1 + 0.2 == 0.3)
print(Decimal("0.1") + Decimal("0.2"), Decimal("0.1") + Decimal("0.2") == Decimal("0.3"))
print(Decimal(0.1))
print(Decimal(0.1) == Decimal("0.1"))
try:
    Decimal("0.1") + 0.2
except TypeError:
    print("TypeError: Decimal и float не смешиваются")
```
```output
0.30000000000000004 False
0.3 True
0.1000000000000000055511151231257827021181583404541015625
False
TypeError: Decimal и float не смешиваются
```

**Пример 2. quantize и режимы округления.**

```python run
from decimal import Decimal, ROUND_HALF_EVEN, ROUND_HALF_UP, ROUND_DOWN

cent = Decimal("0.01")
for s in ("2.665", "2.675", "-2.675", "0.005"):
    x = Decimal(s)
    print(s, x.quantize(cent, rounding=ROUND_HALF_UP), x.quantize(cent, rounding=ROUND_HALF_EVEN), x.quantize(cent, rounding=ROUND_DOWN))

price = Decimal("19.99")
print(price * 3 * Decimal("0.9"))
print((price * 3 * Decimal("0.9")).quantize(cent, rounding=ROUND_HALF_UP))
print(round(2.675, 2))   # float: 2.675 хранится как 2.67499999...
```
```output
2.665 2.67 2.66 2.66
2.675 2.68 2.68 2.67
-2.675 -2.68 -2.68 -2.67
0.005 0.01 0.00 0.00
53.973
53.97
2.67
```

**Пример 3. Контекст точности и деление без потери копеек.**

```python run
from decimal import Decimal, localcontext

print(Decimal(1) / Decimal(3))
with localcontext() as ctx:
    ctx.prec = 5
    print(Decimal(1) / Decimal(3))

total = Decimal("100.00")
n = 3
cents = int(total * 100)
base, rest = divmod(cents, n)
parts = [Decimal(base + (1 if i < rest else 0)) / 100 for i in range(n)]
print(parts, sum(parts))
naive = (total / n).quantize(Decimal("0.01"))
print(naive * n)   # копейка потеряна
```
```output
0.3333333333333333333333333333
0.33333
[Decimal('33.34'), Decimal('33.33'), Decimal('33.33')] 100.00
99.99
```

**Пример 4. Fraction, isclose, fsum.**

```python run
import math
from fractions import Fraction

print(Fraction("1/3") + Fraction("1/6"), Fraction("0.1") + Fraction("0.2") == Fraction(3, 10))
print(Fraction(0.1) == Fraction(1, 10), Fraction(0.1).limit_denominator(100))
print(Fraction(3, 6), Fraction(7, 2) * 2)

print(0.1 + 0.2 == 0.3, math.isclose(0.1 + 0.2, 0.3))
print(math.isclose(1e-12, 0.0), math.isclose(1e-12, 0.0, abs_tol=1e-9))
data = [0.1] * 10
print(math.fsum(data), math.fsum(data) == 1.0)
```
```output
1/2 True
False 1/10
1/2 7
False True
False True
1.0 True
```

**Пример 5. statistics: выборка или генеральная совокупность.**

```python run
import statistics as st

data = [2, 4, 4, 4, 5, 5, 7, 9]
print(st.mean(data), st.median(data), st.mode(data))
print(st.pstdev(data), round(st.stdev(data), 4))
print(round(st.variance(data), 4), st.pvariance(data))
print(st.median([1, 3, 5, 7]), st.fmean([1, 2, 3, 4]))
try:
    st.stdev([5])
except st.StatisticsError:
    print("StatisticsError: для stdev нужно минимум два значения")
```
```output
5 4.5 4
2.0 2.1381
4.5714 4
4.0 2.5
StatisticsError: для stdev нужно минимум два значения
```

**Пример 6. random с seed и secrets.**

```python run
import random
import secrets

rng = random.Random(42)
a = [rng.randint(1, 6) for _ in range(5)]
rng = random.Random(42)
b = [rng.randint(1, 6) for _ in range(5)]
print(a == b, len(a))

items = list(range(10))
r1 = random.Random(7)
r2 = random.Random(7)
x, y = items[:], items[:]
r1.shuffle(x)
r2.shuffle(y)
print(x == y, sorted(x) == items, items)

t1, t2 = secrets.token_hex(8), secrets.token_hex(8)
print(len(t1), t1 == t2)
print(secrets.compare_digest("abc", "abc"))
```
```output
True 5
True True [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
16 False
True
```

<!-- section: mistakes -->
## Типичные ошибки

* **`Decimal(0.1)` вместо `Decimal("0.1")`.** Двоичная ошибка переезжает в Decimal.
* **Смешивание `Decimal` и `float`.** `Decimal("1.1") * 1.1` даёт `TypeError`. Приводите всё к `Decimal` через строку.
* **Ожидание школьного округления.** По умолчанию `ROUND_HALF_EVEN`: `2.665 -> 2.66`. Если бизнес-правило «0.5 вверх», указывайте `ROUND_HALF_UP` явно. Встроенный `round(2.675, 2)` для `float` даёт `2.67` из-за двоичного представления.
* **Округление на каждом шаге.** Ошибки накапливаются; округляйте один раз в конце.
* **Деление суммы поровну с округлением.** `round(total / n, 2) * n` теряет или создаёт копейки. Делите через целые копейки и `divmod`.
* **`except ValueError` вокруг `Decimal(...)`.** `InvalidOperation` - не `ValueError`; ловите его явно.
* **`==` для `float` после вычислений.** Используйте `math.isclose`, а для сравнения с нулём задавайте `abs_tol`.
* **Простое накопление `float` в цикле (`total += x`).** Ошибка растёт; `math.fsum` суммирует без накопления.
* **`stdev` вместо `pstdev` (и наоборот).** Если данные - выборка из большей совокупности, нужен `stdev`; если вы измерили всю совокупность, `pstdev`.
* **`random` для токенов и паролей.** Mersenne Twister предсказуем. Используйте `secrets`.
* **Глобальный `random.seed()` в библиотечном коде.** Он меняет состояние для всей программы. Создавайте собственный `random.Random(seed)`.
* **Ожидание одинакового `shuffle` на разных версиях Python.** Гарантия воспроизводимости есть у `random()`, у остальных методов - нет.
* **`Fraction(0.1)` в расчёте на 1/10.** Это точное значение двоичного `float`; используйте `Fraction("0.1")`.

<!-- section: summary -->
## Итог

* `float` хранит двоичные приближения; для сравнения `math.isclose`, для сумм `math.fsum`.
* Деньги: `Decimal` из строк, один `quantize` в конце, режим округления указан явно (по умолчанию `ROUND_HALF_EVEN`), деление сумм через целые копейки.
* `InvalidOperation` не `ValueError`; `Decimal` и `float` не смешиваются.
* `Fraction` даёт точную рациональную арифметику, но растёт в размере.
* `stdev` - выборочное (n - 1), `pstdev` - генеральное (n); `fmean` быстрее `mean` и возвращает `float`.
* `random.Random(seed)` - воспроизводимые эксперименты; `secrets` - всё, что должно быть непредсказуемым.
* Дальше: [subprocess и logging](lesson:python.stdlib.subprocess-and-logging) - запуск внешних программ и журналирование.
