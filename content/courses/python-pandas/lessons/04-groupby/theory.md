<!-- section: simple -->
## Разделить, посчитать, собрать

Вопрос «сколько выручки в каждом регионе?» — это не операция над строкой и не над таблицей целиком, а над **группами** строк. У pandas для этого один шаблон, **split-apply-combine**:

1. **split** — разделить строки на группы по значению ключа (`region`);
2. **apply** — внутри каждой группы вычислить что-то (сумму, среднее, ранг);
3. **combine** — собрать результаты в одну таблицу.

Всё это делает `df.groupby("region")`. Важно понимать, что вы получаете на выходе: одну строку на группу (агрегация), строку на каждую исходную строку (преобразование, например доля внутри региона) или произвольную форму (`apply`). Эти три случая решают разные задачи, и путать их — главный источник ошибок. Вторая ловушка: строки, у которых в ключе пропуск `NaN`, по умолчанию **исчезают** из результата, и суммы по группам перестают сходиться с общей суммой.

<!-- section: precise -->
## Агрегация, преобразование, apply

* **Агрегация** (`agg`, `sum`, `mean`, `count`, `size`, `nunique`, `idxmax`): одна строка на группу, индекс результата — ключи. `count` не считает пропуски, `size` считает все строки группы.
* **Именованные агрегаты** (named aggregation): `df.groupby("region").agg(orders=("sale_id", "count"), revenue=("revenue", "sum"))` — имена столбцов результата задаются явно, это читаемее словарей со вложенностью.
* **Преобразование** (`transform`): результат имеет **тот же индекс, что и исходная таблица**: `df.groupby("region")["revenue"].transform("sum")` — суммарная выручка региона, повторённая для каждой строки. Через него считают долю в группе, центрирование, заполнение пропусков статистикой группы. Также `rank`, `cumsum`, `shift`, `pct_change` работают внутри групп и возвращают столбец того же размера.
* **apply** принимает функцию от подтаблицы группы и сам решает, как собрать результат. Он гибок, но медленнее встроенных методов, а форма результата зависит от того, что вернула функция. Если задачу можно выразить через `agg` или `transform`, выбирайте их.
* `groupby(["a", "b"])` — составной ключ, индекс результата — `MultiIndex`; `as_index=False` или `reset_index()` делают ключи обычными столбцами.

## Параметры, влияющие на результат

* `dropna=False` включает группу `NaN`; по умолчанию (`dropna=True`) строки с пропуском в ключе отбрасываются молча.
* `sort=False` сохраняет порядок первого появления ключей вместо сортировки по ключу (ключи сортируются по умолчанию).
* `observed` относится к категориальным ключам: показывать ли только встретившиеся категории или все объявленные. В установленной версии (pandas 3.0.6) по умолчанию в результате видны только встретившиеся (`observed=True`); пример ниже это проверяет.
* Группировка по нескольким ключам вместе с `unstack()` превращает уровень индекса в столбцы (подробнее в уроке про reshape).

<!-- section: examples -->
## Примеры

Во всех примерах небольшая таблица продаж, созданная в коде.

**Пример 1. Именованные агрегаты.**

```python run
import pandas as pd

pd.set_option("display.width", 80)
df = pd.DataFrame(
    {
        "region": ["north", "south", "north", "south", "north"],
        "qty": [1, 2, 3, 4, 5],
        "revenue": [10.0, 40.0, 30.0, 20.0, 60.0],
    }
)
out = df.groupby("region").agg(
    orders=("qty", "count"),
    units=("qty", "sum"),
    avg_ticket=("revenue", "mean"),
)
print(out)
print(out.sort_values("avg_ticket", ascending=False).index.tolist())
```
```output
        orders  units  avg_ticket
region                           
north        3      9   33.333333
south        2      6   30.000000
['north', 'south']
```

**Пример 2. transform возвращает столбец того же размера.**

```python run
import pandas as pd

df = pd.DataFrame(
    {"region": ["north", "south", "north", "south", "north"], "revenue": [10.0, 40.0, 30.0, 20.0, 60.0]},
    index=list("abcde"),
)
total = df.groupby("region")["revenue"].transform("sum")
print(total.tolist())
df["share"] = df["revenue"] / total
print(df)
print(df.groupby("region")["share"].sum().round(9).tolist())
```
```output
[100.0, 60.0, 100.0, 60.0, 100.0]
  region  revenue     share
a  north     10.0  0.100000
b  south     40.0  0.666667
c  north     30.0  0.300000
d  south     20.0  0.333333
e  north     60.0  0.600000
[1.0, 1.0]
```

Каждая строка получила сумму своего региона, а доли внутри региона дают в сумме 1.

**Пример 3. agg и apply: разная форма результата.**

```python run
import pandas as pd

df = pd.DataFrame(
    {"region": ["north", "south", "north", "south"], "revenue": [10.0, 40.0, 30.0, 20.0]}
)
print(df.groupby("region")["revenue"].sum().to_dict())
by_apply = df.groupby("region")["revenue"].apply(lambda s: s - s.mean())
print(by_apply.index.nlevels, by_apply.shape)
by_transform = df.groupby("region")["revenue"].transform(lambda s: s - s.mean())
print(by_transform.index.nlevels, by_transform.shape, by_transform.tolist())
```
```output
{'north': 40.0, 'south': 60.0}
2 (4,)
1 (4,) [-10.0, 10.0, 10.0, -10.0]
```

`apply` вернул ряд с дополнительным уровнем индекса (ключ группы), а `transform` — столбец в исходном порядке.

**Пример 4. Строки с NaN в ключе по умолчанию пропадают.**

```python run
import pandas as pd

df = pd.DataFrame({"region": ["north", None, "north", "south"], "revenue": [10.0, 5.0, 20.0, 7.0]})
print("всего:", df["revenue"].sum())
print(df.groupby("region")["revenue"].sum())
print(df.groupby("region")["revenue"].sum().sum())
print(df.groupby("region", dropna=False)["revenue"].sum())
```
```output
всего: 42.0
region
north    30.0
south     7.0
Name: revenue, dtype: float64
37.0
region
north    30.0
south     7.0
NaN       5.0
Name: revenue, dtype: float64
```

Сумма по группам 37.0 меньше общей 42.0: выручка из строки с пропущенным регионом молча потерялась. С `dropna=False` она видна в группе `NaN`.

**Пример 5. sort и категориальный ключ.**

```python run
import pandas as pd

df = pd.DataFrame({"k": ["b", "a", "b", "c"], "v": [1, 2, 3, 4]})
print(df.groupby("k")["v"].sum().index.tolist())
print(df.groupby("k", sort=False)["v"].sum().index.tolist())

cat = pd.DataFrame({"k": pd.Categorical(["a", "a"], categories=["a", "b"]), "v": [1, 2]})
print(cat.groupby("k")["v"].sum().index.tolist())
print(cat.groupby("k", observed=False)["v"].sum().to_dict())
```
```output
['a', 'b', 'c']
['b', 'a', 'c']
['a']
{'a': 3, 'b': 0}
```

<!-- section: mistakes -->
## Типичные ошибки

* **Не заметить потерю строк с пропуском в ключе.** Проверяйте, что сумма по группам равна общей, или ставьте `dropna=False`.
* **Использовать `apply` там, где достаточно `agg` / `transform`.** Это медленнее и даёт менее предсказуемую форму результата.
* **Ждать от `agg` столбец размера исходной таблицы.** Он даёт строку на группу; для «значение группы в каждой строке» нужен `transform`.
* **Путать `count` и `size`.** `count` не считает пропуски, `size` считает все строки группы.
* **Забыть, что ключи становятся индексом.** Дальше обращаться к ним как к столбцу можно только после `reset_index()` (или `as_index=False`).
* **Не думать о порядке групп.** По умолчанию результат отсортирован по ключу; для порядка первого появления есть `sort=False`.
* **Складывать доли разных группировок.** Доля внутри региона и доля от общей суммы — разные числа; фиксируйте, от чего считаете.

<!-- section: summary -->
## Итоги

* `groupby` реализует split-apply-combine; `agg` даёт строку на группу, `transform` — столбец исходного размера, `apply` — произвольную форму.
* Именованные агрегаты `agg(name=(column, func))` делают результат читаемым.
* Строки с `NaN` в ключе по умолчанию исчезают; `dropna=False` сохраняет их как группу.
* `sort` управляет порядком групп, `observed` — показом неиспользованных категорий (в установленной версии показаны только встретившиеся).
* Проверяйте результат контрольной суммой: итог по группам должен совпасть с общим.

Дальше: [объединение и reshape](lesson:python.pandas.joins-and-reshaping). Основа: [выбор и присваивание](lesson:python.pandas.selection-and-assignment).
