<!-- section: simple -->
## Сырые данные всегда грязные

Файл CSV — это просто текст. pandas должен сам решить, что в столбце лежит: числа, даты или строки; что считать пропуском; где заканчивается число и начинается мусор вроде `N/A`. Угадывает он неплохо, но не идеально: почтовый индекс `00393` превратится в число `393`, дата останется строкой, а один маркер `-` в столбце с числами превратит весь столбец в строки.

Поэтому чтение данных — это договор: вы явно говорите, какие типы вы ожидаете (`dtype`), какие столбцы — даты (`parse_dates`) и какие строки — пропуски (`na_values`). После чтения идёт очистка: посчитать пропуски, решить, что с ними делать (удалить, заполнить, оставить как есть), убрать дубликаты и привести типы. Каждое из этих решений меняет результат анализа, поэтому его нужно принять осознанно, а не по умолчанию.

<!-- section: precise -->
## Чтение: dtype, parse_dates, na_values

`pd.read_csv(source, ...)` принимает путь, файловый объект или `io.StringIO`. Ключевые параметры:

* `dtype={"sku": "str"}` — явный тип столбца; нужен, чтобы сохранить ведущие нули в кодах и идентификаторах;
* `parse_dates=["order_date"]` — разобрать столбцы как даты (получится `datetime64`); без этого дата читается как строка;
* `na_values=["N/A", "-"]` — дополнительные строки, считающиеся пропуском. Стандартные маркеры (пустая ячейка, `NA`, `N/A`, `null`, `NaN` и другие) распознаются и без этого параметра; `keep_default_na=False` отключает стандартный список;
* `usecols`, `nrows`, `sep`, `encoding` — читать только нужное и в нужном формате.

Если в столбце с числами остался нераспознанный маркер (например, `-`), столбец читается как строки (`str`). Приводить его к числу можно позже: `pd.to_numeric(s, errors="coerce")` превращает нечисловые значения в `NaN` (используйте осознанно: так можно спрятать ошибку), `pd.to_datetime(s)` разбирает даты.

## Пропуски: NaN, None, pd.NA

* **`NaN`** — вещественное число (`float("nan")`); именно им pandas представляет пропуск в `float64` и (в pandas 3.0) в строковом dtype `str`. `NaN != NaN`, поэтому сравнивать с ним через `==` нельзя;
* **`None`** — объект Python; при создании Series он превращается в `NaN` в числовых и строковых столбцах;
* **`pd.NA`** — единый «скалярный пропуск» для масштабируемых (nullable) dtype: `Int64`, `boolean`, `string`. Он позволяет иметь целый столбец с пропусками без перехода к `float64`; сравнение с `pd.NA` даёт `pd.NA`, а не `False`.

Проверять пропуск надо через `isna()` / `notna()` (для Series и DataFrame) или `pd.isna(x)` для скаляра: они понимают все три вида. Столбец `int64` пропуск вместить не может, поэтому `NaN` превращает его в `float64`, а приведение обратно `astype("int64")` при наличии пропусков бросает ошибку; для целых с пропусками есть `Int64`.

## Что делать с пропусками

* `dropna(subset=[...])` — убрать строки; просто и честно, но теряются данные и может смещаться выборка (если пропуски не случайны);
* `fillna(value)` — заполнить константой, средним, медианой; заполнение общим средним скрывает различия между группами, медиана группы (`groupby(...).transform("median")`) обычно честнее;
* оставить как есть и учитывать: `sum`, `mean`, `count` по умолчанию пропускают `NaN`, но `count` и `len` — разные числа.

Любая политика должна быть записана в коде и проверена тестом.

## Дубликаты и типы

`df.duplicated()` отмечает повторы (по умолчанию помечается каждая строка, кроме первой, совпавшая со всеми столбцами), `df.drop_duplicates(subset=..., keep="first")` их убирает. После удаления строк индекс остаётся «дырявым»: `reset_index(drop=True)` возвращает 0..n-1. Типы приводят через `astype`, `pd.to_numeric`, `pd.to_datetime`; метод `astype` возвращает новый объект и не меняет исходный.

<!-- section: examples -->
## Примеры

Данные в примерах заданы строкой и читаются через `io.StringIO`, поэтому примеры не зависят от файлов.

**Пример 1. Что получается без параметров и с ними.**

```python run
import io
import pandas as pd

text = """order_id,sku,order_date,qty
1,00393,2025-01-02,10
2,00379,2025-01-03,-
3,00159,2025-01-05,4
"""
raw = pd.read_csv(io.StringIO(text))
print(raw.dtypes)
fixed = pd.read_csv(
    io.StringIO(text),
    dtype={"sku": "str"},
    parse_dates=["order_date"],
    na_values=["-"],
)
print(fixed.dtypes)
print(fixed)
```
```output
order_id      int64
sku           int64
order_date      str
qty             str
dtype: object
order_id               int64
sku                      str
order_date    datetime64[us]
qty                  float64
dtype: object
   order_id    sku order_date   qty
0         1  00393 2025-01-02  10.0
1         2  00379 2025-01-03   NaN
2         3  00159 2025-01-05   4.0
```

Без параметров `sku` потерял ведущие нули и стал числом, дата осталась строкой, а один нестандартный маркер `-` сделал весь столбец `qty` строковым (в нём остались строки). Заметьте, что в этом примере `qty` у `fixed` стал `float64`: `NaN` вытеснил целые.

**Пример 2. NaN, None и pd.NA.**

```python run
import numpy as np
import pandas as pd

nan = float("nan")
print(nan == nan, pd.isna(nan), pd.isna(None), pd.isna(pd.NA))
print(pd.NA == 1)

s = pd.Series([1, None, 3])
print(s.dtype, s.tolist())
n = pd.Series([1, None, 3], dtype="Int64")
print(n.dtype, n.tolist())
t = pd.Series(["a", None, np.nan])
print(t.dtype, t.isna().tolist())
try:
    s.astype("int64")
except Exception as exc:
    print(type(exc).__name__)
print(s.astype("Int64").tolist())
```
```output
False True True True
<NA>
float64 [1.0, nan, 3.0]
Int64 [1, <NA>, 3]
str [False, True, True]
IntCastingNaNError
[1, <NA>, 3]
```

**Пример 3. Пропуски: считаем, удаляем, заполняем.**

```python run
import pandas as pd

pd.set_option("display.width", 80)
df = pd.DataFrame(
    {
        "product": ["pen", "pen", "lamp", "lamp", "lamp"],
        "price": [1.5, None, 24.0, 20.0, None],
    }
)
print(df["price"].isna().sum(), df["price"].count(), len(df))
print(df.dropna().shape)
print(df["price"].fillna(df["price"].mean()).round(2).tolist())
by_group = df["price"].fillna(df.groupby("product")["price"].transform("median"))
print(by_group.tolist())
```
```output
2 3 5
(3, 2)
[1.5, 15.17, 24.0, 20.0, 15.17]
[1.5, 1.5, 24.0, 20.0, 22.0]
```

Среднее по всем товарам (15.17) подставило в цену ручки и лампы одно число. Медиана внутри товара сохраняет различие: ручка получила 1.5, лампа 22.0.

**Пример 4. Дубликаты и индекс.**

```python run
import pandas as pd

df = pd.DataFrame({"id": [1, 2, 2, 3, 3], "v": ["a", "b", "b", "c", "x"]})
print(df.duplicated().tolist())
once = df.drop_duplicates()
print(once.index.tolist())
print(once.reset_index(drop=True).index.tolist())
print(df.drop_duplicates(subset="id", keep="last")["v"].tolist())
```
```output
[False, False, True, False, False]
[0, 1, 3, 4]
[0, 1, 2, 3]
['a', 'b', 'x']
```

**Пример 5. Приведение типов.**

```python run
import pandas as pd

s = pd.Series(["10", "20", "x", "40"])
print(pd.to_numeric(s, errors="coerce").tolist())
print(pd.to_datetime(pd.Series(["2025-01-02", "2025-02-03"])).dt.month.tolist())
q = pd.Series([3.0, 4.0, 5.0])
print(q.astype("int64").dtype, q.dtype)
```
```output
[10.0, 20.0, nan, 40.0]
[1, 2]
int64 float64
```

<!-- section: mistakes -->
## Типичные ошибки

* **Читать идентификаторы без `dtype`.** Коды вроде `00393`, номера и ИНН превращаются в числа без ведущих нулей.
* **Не указать `na_values`.** Один нестандартный маркер вроде `-` в числовом столбце делает весь столбец строковым, и суммы перестают считаться.
* **Сравнивать с пропуском через `==`.** `s == np.nan` всегда `False`; используйте `isna()`.
* **Заполнять пропуски общим средним, не подумав.** Это смещает распределение и скрывает различия групп; фиксируйте политику и проверяйте её.
* **`errors="coerce"` как способ «заставить работать».** Он молча превращает мусор в `NaN`; посчитайте, сколько значений стало пропусками.
* **Забыть сбросить индекс после `drop_duplicates` / `dropna`.** Дальше `loc[0]` может не найти строку.
* **Считать, что `astype("int64")` справится с пропусками.** Он бросает ошибку; нужен `Int64` или явное решение, что делать со строками.

<!-- section: summary -->
## Итоги

* При чтении CSV явно задавайте `dtype`, `parse_dates` и `na_values`; идентификаторы читайте строками.
* Пропуск проверяйте через `isna()`; `NaN` превращает `int64` в `float64`, для целых с пропусками есть `Int64` и `pd.NA`.
* Удалять или заполнять пропуски — решение с последствиями; заполнение по группе обычно точнее общего среднего.
* `drop_duplicates` оставляет «дырявый» индекс; после очистки сбрасывайте его.
* Очищающие функции возвращают новый объект и не меняют вход.

Дальше: [выбор, фильтрация и присваивание](lesson:python.pandas.selection-and-assignment). Повторите [Series и DataFrame](lesson:python.pandas.series-and-dataframe).
