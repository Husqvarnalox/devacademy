<!-- section: simple -->
## Выбрать строки, изменить значения

Большая часть работы с таблицей — это два действия: **выбрать** нужные строки и столбцы («заказы из региона north с количеством от 10») и **изменить** значения («уценить эти строки на 10%»). Выбор делается булевой маской: сравнение столбца с числом даёт столбец из `True`/`False`, и этим столбцом, как решетом, отбираются строки.

Сложность начинается с вопроса: *изменяю я исходную таблицу или копию?* В старых версиях pandas ответ зависел от того, вернул ли выбор «представление» или копию, и код вида `df[df.qty > 5]["price"] = 0` иногда работал, а иногда нет. В pandas 3.0 правила стали простыми: любой результат выбора ведёт себя как независимая копия (**Copy-on-Write**), поэтому изменение результата никогда не меняет исходную таблицу, а изменять исходную нужно одним присваиванием `df.loc[маска, "столбец"] = значение`.

<!-- section: precise -->
## Маски, isin, between, query

* Сравнение (`df["qty"] >= 10`) даёт булев Series; объединяйте маски операторами `&` (и), `|` (или), `~` (не), **каждое сравнение в скобках**: приоритет `&` выше, чем у `>=`. Ключевые слова `and`/`or` не работают с Series и бросают `ValueError`.
* `s.isin([...])` — принадлежность множеству, `s.between(a, b)` — диапазон (по умолчанию границы включаются), `s.isna()` — пропуски.
* `df.query("qty >= 10 and region == 'north'")` — то же строкой; внешние переменные подставляются через `@name`. Внутри `query` допустимы `and`/`or`.
* `df.loc[маска, ["a", "b"]]` — строки по маске и столбцы по именам; `df.iloc[0:5, 0:2]` — по позициям.
* `df.assign(total=df["qty"] * df["price"])` возвращает **новый** DataFrame с добавленным столбцом, исходный не меняется.

## Присваивание и chained assignment

Правильный способ изменить часть таблицы — **один** вызов `__setitem__` на исходном объекте: `df.loc[mask, "price"] = df.loc[mask, "price"] * 0.9`.

**Цепочка** `df[mask]["price"] = 0` состоит из двух шагов: сначала `df[mask]` создаёт результат, затем присваивание идёт в этот промежуточный результат. Раньше это иногда меняло `df` (и тогда pandas выдавал `SettingWithCopyWarning`), иногда нет. В установленной версии pandas 3.0 по правилам Copy-on-Write результат любого выбора ведёт себя как копия, поэтому такая цепочка **никогда не меняет** `df`, а pandas выдаёт предупреждение `ChainedAssignmentError`. `SettingWithCopyWarning` в 3.0 удалён.

## Copy-on-Write в pandas 3.0

Согласно примечаниям к выпуску 3.0.0, любая индексация или метод, возвращающие новый DataFrame или Series, ведут себя как копия: изменение результата не затрагивает исходный объект и наоборот. Внутри pandas по возможности делится памятью (копирование откладывается до первой записи), поэтому защитный `.copy()` для этого больше не нужен. Опция `mode.copy_on_write` объявлена устаревшей: отключить Copy-on-Write нельзя.

Важное следствие: присваивание `b = a` не копирует, это то же имя того же объекта. `df2 = df` и последующее `df2.loc[...] = ...` изменяют **один и тот же** DataFrame. А вот `df2 = df[df["a"] > 1]` — независимый объект.

При присваивании значения, не подходящего по типу столбца (например, `1.5` в столбец `int64`), pandas 3.0 в проверенном примере ниже бросает `TypeError`, а не меняет тип молча.

<!-- section: examples -->
## Примеры

В примерах `pd.DataFrame` собран прямо в коде.

**Пример 1. Булевы маски и скобки.**

```python run
import pandas as pd

df = pd.DataFrame(
    {"qty": [1, 12, 5, 20], "region": ["north", "north", "south", "north"]},
    index=list("pqrs"),
)
mask = (df["qty"] >= 10) & (df["region"] == "north")
print(mask.tolist())
print(df[mask])
print(df[~mask].index.tolist())
try:
    df[(df["qty"] >= 10) and (df["region"] == "north")]
except ValueError as exc:
    print("ValueError:", str(exc)[:46])
```
```output
[False, True, False, True]
   qty region
q   12  north
s   20  north
['p', 'r']
ValueError: The truth value of a Series is ambiguous. Use 
```

**Пример 2. isin, between и query.**

```python run
import pandas as pd

df = pd.DataFrame(
    {"qty": [1, 12, 5, 20], "region": ["north", "east", "south", "north"]},
)
limit = 4
print(df[df["region"].isin(["north", "east"])].index.tolist())
print(df[df["qty"].between(5, 12)].index.tolist())
print(df.query("qty > @limit and region != 'south'").index.tolist())
print(df.loc[df["qty"] > limit, ["qty"]].shape)
```
```output
[0, 1, 3]
[1, 2]
[1, 3]
(3, 1)
```

**Пример 3. Правильное присваивание через loc.**

```python run
import pandas as pd

df = pd.DataFrame({"qty": [5, 15, 20], "price": [10.0, 10.0, 10.0]})
mask = df["qty"] >= 10
df.loc[mask, "price"] = df.loc[mask, "price"] * 0.9
print(df["price"].tolist())

bigger = df.assign(total=df["qty"] * df["price"])
print(bigger.columns.tolist(), df.columns.tolist())
```
```output
[10.0, 9.0, 9.0]
['qty', 'price', 'total'] ['qty', 'price']
```

**Пример 4. Chained assignment не меняет таблицу.**

```python run
import warnings
import pandas as pd

df = pd.DataFrame({"a": [1, 2, 3], "b": [0, 0, 0]})
with warnings.catch_warnings(record=True) as caught:
    warnings.simplefilter("always")
    df[df["a"] > 1]["b"] = 99
    df["b"][0] = 5
print(df["b"].tolist())
print(sorted({type(w.message).__name__ for w in caught}))

df.loc[df["a"] > 1, "b"] = 99
print(df["b"].tolist())
```
```output
[0, 0, 0]
['ChainedAssignmentError']
[0, 99, 99]
```

**Пример 5. Copy-on-Write: копия, а не представление.**

```python run
import pandas as pd

df = pd.DataFrame({"a": [1, 2, 3], "b": ["x", "y", "z"]})
alias = df
alias.loc[0, "a"] = 100
print("alias это тот же объект:", df.loc[0, "a"])

part = df[df["a"] > 1]
part.loc[1, "a"] = -5
print("исходный не изменился:", df.loc[1, "a"], "| часть:", part.loc[1, "a"])

col = df["a"]
col.iloc[0] = 7
print("столбец, взятый из df, тоже независим:", df.loc[0, "a"])

x = pd.DataFrame({"q": [1, 2, 3]})
try:
    x.loc[x["q"] > 1, "q"] = 1.5
except TypeError as exc:
    print("TypeError:", str(exc)[:30])
```
```output
alias это тот же объект: 100
исходный не изменился: 2 | часть: -5
столбец, взятый из df, тоже независим: 100
TypeError: Invalid value '1.5' for dtype 
```

<!-- section: mistakes -->
## Типичные ошибки

* **`and` / `or` вместо `&` / `|`.** Бросает `ValueError: The truth value of a Series is ambiguous`.
* **Забыть скобки вокруг сравнений.** `df.a > 1 & df.b == 2` разбирается как цепочка сравнений с `&` и даёт ошибку или неверную маску.
* **Присваивать через цепочку `df[mask]["col"] = x` или `df["col"][i] = x`.** Таблица не изменится (в pandas 3.0 — с предупреждением `ChainedAssignmentError`); пишите `df.loc[mask, "col"] = x`.
* **Ставить защитный `.copy()` везде «на всякий случай».** В pandas 3.0 он не нужен для безопасности (по примечаниям к выпуску); `copy()` оправдан, когда код должен работать и в более старых версиях или вы хотите явно показать намерение.
* **Считать `b = a` копией.** Это второе имя того же DataFrame; изменения видны через оба.
* **Менять входной DataFrame внутри функции.** Функция должна вернуть новый объект: `out = df.copy()` или `assign`.
* **Ждать, что `query` видит переменные Python без `@`.** Внешняя переменная вводится как `@threshold`.

<!-- section: summary -->
## Итоги

* Выбор — булева маска: `&`, `|`, `~` и скобки; `isin`, `between`, `query` — удобные формы.
* Изменять часть таблицы нужно одним присваиванием `df.loc[mask, "col"] = value`.
* В установленном pandas 3.0 Copy-on-Write: результат любого выбора ведёт себя как копия, цепочка `df[mask]["col"] = x` ничего не меняет и даёт `ChainedAssignmentError`.
* `b = a` не копирует; функции должны возвращать новый объект и не мутировать вход.
* Несовместимое по типу значение в `loc` бросает `TypeError`.

Дальше: [группировка](lesson:python.pandas.groupby). Предыдущее: [чтение и очистка](lesson:python.pandas.reading-and-cleaning).
