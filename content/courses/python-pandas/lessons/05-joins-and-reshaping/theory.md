<!-- section: simple -->
## Две таблицы в одну и широкая в длинную

Данные редко лежат в одной таблице. Заказы хранят `customer_id`, а имя и страна клиента — в таблице клиентов. Чтобы получить заказ вместе со страной, таблицы **объединяют по ключу** (`merge`) — это тот же JOIN из SQL. Тут два вопроса, на которые нужно ответить заранее:

1. **Что делать со строками без пары?** `inner` оставит только совпавшие и молча потеряет остальные, `left` сохранит все строки левой таблицы.
2. **Сколько строк получится?** Если ключ в правой таблице не уникален, каждая левая строка размножится. Число строк результата вырастет, а суммы по заказам станут завышенными, при этом ошибки не будет.

Вторая половина урока — **reshape**: превращение «длинной» таблицы (одна строка на измерение) в «широкую» (сводная таблица) и обратно. `pivot_table` собирает сводную, `melt` разворачивает обратно, `concat` просто склеивает таблицы друг с другом.

<!-- section: precise -->
## merge: how, on, validate, indicator

`left.merge(right, on="key", how="left")` (или `pd.merge(left, right, ...)`):

* `how="inner"` — только ключи, которые есть в обеих; `"left"` / `"right"` — все строки одной стороны; `"outer"` — все строки обеих; `"cross"` — все пары.
* Ключ можно задать `on=` (одинаковое имя), `left_on=` / `right_on=` или индексами (`left_index=True`). Одноимённые неключевые столбцы получают суффиксы `_x`, `_y` или заданные в `suffixes=`.
* **`validate`** проверяет ожидаемую связь и бросает `pandas.errors.MergeError`, если она нарушена: `"one_to_one"`, `"one_to_many"`, `"many_to_one"`, `"many_to_many"`. Для справочника клиентов правильный выбор — `many_to_one`: он защитит от размножения строк.
* **`indicator=True`** добавляет столбец `_merge` со значениями `both`, `left_only`, `right_only`; на нём строят отчёты о несовпавших ключах («сироты»).
* Порядок строк результата `left`-merge соответствует порядку левой таблицы; в остальных вариантах порядок зависит от `how`, и на него полагаться не следует.

## concat

`pd.concat([a, b])` склеивает **по строкам** (по умолчанию `axis=0`) и сохраняет исходные метки индекса, поэтому после склейки метки могут повторяться; `ignore_index=True` перенумеровывает. `pd.concat([a, b], axis=1)` склеивает по столбцам и **выравнивает по индексу** (как арифметика из первого урока). Это не JOIN по ключу.

## pivot, pivot_table, melt

* `df.pivot(index, columns, values)` — просто перестановка: каждая пара (index, columns) должна встречаться **не более одного раза**, иначе `ValueError` (дубликаты).
* `df.pivot_table(index, columns, values, aggfunc="mean", fill_value=...)` — агрегирует: несколько строк на одну клетку складываются выбранной функцией; по умолчанию `aggfunc` — среднее. Пустые клетки дают `NaN`, заменить их можно `fill_value`. Строки с `NaN` в ключе в таблицу не попадают.
* `df.melt(id_vars=[...], var_name=..., value_name=...)` — обратная операция: столбцы становятся значениями. Перед `melt` у сводной таблицы обычно вызывают `reset_index()`.

<!-- section: examples -->
## Примеры

**Пример 1. inner и left: потерянные строки.**

```python run
import pandas as pd

pd.set_option("display.width", 80)
orders = pd.DataFrame({"order_id": [1, 2, 3], "cid": ["a", "b", "z"], "amount": [10, 20, 30]})
customers = pd.DataFrame({"cid": ["a", "b", "c"], "name": ["Ann", "Bob", "Cy"]})
inner = orders.merge(customers, on="cid")
left = orders.merge(customers, on="cid", how="left")
print(len(orders), len(inner), len(left))
print(left)
```
```output
3 2 3
   order_id cid  amount name
0         1   a      10  Ann
1         2   b      20  Bob
2         3   z      30  NaN
```

`inner` молча потерял заказ клиента `z`: общая сумма заказов уменьшилась на 30, и никакой ошибки не было.

**Пример 2. Дубликаты в ключе размножают строки; validate их ловит.**

```python run
import pandas as pd
from pandas.errors import MergeError

orders = pd.DataFrame({"order_id": [1, 2], "cid": ["a", "b"], "amount": [10, 20]})
customers = pd.DataFrame({"cid": ["a", "a", "b"], "name": ["Ann", "Ann (old)", "Bob"]})
joined = orders.merge(customers, on="cid", how="left")
print(len(orders), len(joined), joined["amount"].sum())
try:
    orders.merge(customers, on="cid", how="left", validate="many_to_one")
except MergeError as exc:
    print("MergeError:", str(exc).splitlines()[0])
```
```output
2 3 40
MergeError: Merge keys are not unique in right dataset; not a many-to-one merge
```

Заказ клиента `a` дублировался: теперь три строки вместо двух, а сумма стала 40 вместо 30.

**Пример 3. indicator: найти сирот.**

```python run
import pandas as pd

orders = pd.DataFrame({"order_id": [1, 2, 3], "cid": ["a", "b", "z"]})
customers = pd.DataFrame({"cid": ["a", "b", "c"]})
m = orders.merge(customers, on="cid", how="outer", indicator=True)
print(m)
print(m["_merge"].value_counts().sort_index().to_dict())
print(m.loc[m["_merge"] == "left_only", "order_id"].astype("int64").tolist())
```
```output
   order_id cid      _merge
0       1.0   a        both
1       2.0   b        both
2       NaN   c  right_only
3       3.0   z   left_only
{'left_only': 1, 'right_only': 1, 'both': 2}
[3]
```

**Пример 4. concat по строкам и по столбцам.**

```python run
import pandas as pd

a = pd.DataFrame({"v": [1, 2]}, index=["x", "y"])
b = pd.DataFrame({"v": [3, 4]}, index=["y", "z"])
print(pd.concat([a, b]).index.tolist())
print(pd.concat([a, b], ignore_index=True).index.tolist())
print(pd.concat([a, b], axis=1, keys=["a", "b"]))
```
```output
['x', 'y', 'y', 'z']
[0, 1, 2, 3]
     a    b
     v    v
x  1.0  NaN
y  2.0  3.0
z  NaN  4.0
```

**Пример 5. pivot, pivot_table и melt.**

```python run
import pandas as pd

pd.set_option("display.width", 80)
sales = pd.DataFrame(
    {
        "country": ["DE", "DE", "DE", "FR"],
        "segment": ["retail", "retail", "wholesale", "retail"],
        "amount": [10, 5, 7, 20],
    }
)
try:
    sales.pivot(index="country", columns="segment", values="amount")
except ValueError as exc:
    print("pivot:", str(exc))
wide = sales.pivot_table(index="country", columns="segment", values="amount", aggfunc="sum", fill_value=0)
print(wide)
long = wide.reset_index().melt(id_vars="country", var_name="segment", value_name="amount")
print(long)
```
```output
pivot: Index contains duplicate entries, cannot reshape
segment  retail  wholesale
country                   
DE           15          7
FR           20          0
  country    segment  amount
0      DE     retail      15
1      FR     retail      20
2      DE  wholesale       7
3      FR  wholesale       0
```

`pivot` отказался: пара (DE, retail) встречается дважды. `pivot_table` сложил 10 и 5, а пустую клетку (FR, wholesale) заполнил нулём.

<!-- section: mistakes -->
## Типичные ошибки

* **`inner` вместо `left`.** Строки без пары исчезают без предупреждения; сравнивайте `len` до и после и считайте сироты через `indicator=True`.
* **Не проверять уникальность ключа справочника.** Дубликаты размножают строки и завышают суммы; задайте `validate="many_to_one"`.
* **Склеивать таблицы по столбцам через `concat(axis=1)` вместо `merge`.** `concat` выравнивает по индексу, а не по ключевому столбцу.
* **Забыть `ignore_index=True` после `concat`.** Повторяющиеся метки индекса потом ломают `loc`.
* **Использовать `pivot` на данных с дубликатами.** Нужен `pivot_table` с осознанно выбранным `aggfunc`; по умолчанию это среднее, а не сумма.
* **Не заметить `NaN`-клетки сводной.** Пустая комбинация — это не нулевая продажа; решите, нужен ли `fill_value`.
* **Не учитывать, что строки с пропуском в ключе пропадают из `pivot_table`.** Как и в `groupby`, проверяйте контрольную сумму.

<!-- section: summary -->
## Итоги

* `merge` — JOIN по ключу: выбирайте `how` осознанно, `left` не теряет строки левой таблицы.
* `validate="many_to_one"` защищает от размножения строк, `indicator=True` показывает несовпавшие ключи.
* `concat` склеивает (по строкам или по индексу), а не соединяет по ключу.
* `pivot` только переставляет и требует уникальных пар, `pivot_table` агрегирует (`aggfunc`, `fill_value`), `melt` разворачивает обратно.
* Контроль: число строк и общая сумма до и после объединения.

Дальше: [производительность и память](lesson:python.pandas.performance-and-memory). Основа: [groupby](lesson:python.pandas.groupby).
