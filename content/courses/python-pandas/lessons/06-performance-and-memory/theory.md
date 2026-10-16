<!-- section: simple -->
## Быстро и компактно: пусть работает векторный код

pandas быстр, пока работа идёт **целыми столбцами**: сложение двух столбцов, сравнение, `groupby().sum()` выполняются внутри оптимизированного кода над массивами NumPy. Как только вы пишете `for` по строкам или `df.apply(f, axis=1)`, функция Python вызывается **на каждой строке**, и вы платите за накладные расходы интерпретатора миллионы раз. Правило: сначала ищите векторное выражение, и только если его нет — цикл или `apply`.

Второй ресурс — память. Столбец из миллиона строковых значений «ok»/«warn»/«fault» хранит миллион отдельных строк, хотя различных значений три. **Категориальный dtype** (`category`) хранит три строки и по одному маленькому целому коду на строку. Столбцы `int64` с числами до тысячи можно хранить как `int16`. Чтобы видеть реальную картину, измеряйте: `memory_usage(deep=True)`.

И третье — читаемость: цепочка методов (`query → assign → groupby → agg`) превращает преобразование в один понятный конвейер без промежуточных переменных и без мутации входа.

<!-- section: precise -->
## Векторизация против apply и iterrows

* Векторная операция (`df["a"] * df["b"]`, `np.where`, `Series.map(dict)`, `np.select`) обрабатывает столбец целиком в скомпилированном коде.
* `df.apply(f, axis=1)`, `Series.apply(f)`, `iterrows()` и `itertuples()` вызывают код Python по одному разу на строку. Они нужны, когда логика действительно не выражается столбцовыми операциями, но платить за них приходится временем: разрыв растёт с числом строк на порядки.
* Условная логика: `np.where(cond, a, b)` для двух вариантов, `np.select([c1, c2], [v1, v2], default=...)` для нескольких, `Series.map({"ok": 0.1, ...})` для подстановки по справочнику, `Series.where` и `mask` для замены по условию.
* Измеряйте на своих данных, а не по «известному» совету: сравнивайте варианты на таблице реального размера (`timeit`, `time.perf_counter`).

## Память: dtype, deep, category

* `df.memory_usage()` по умолчанию считает только занятые столбцами буферы; для строк это указатели. `memory_usage(deep=True)` считает ещё и сами объекты-строки, поэтому для текстовых столбцов разница бывает большой.
* Числовой dtype задаёт размер на элемент: `int64` — 8 байт, `int16` — 2 байта, `float32` — 4. `pd.to_numeric(s, downcast="integer")` выбирает наименьший целый тип, вмещающий данные. Сужайте только если уверены в диапазоне новых данных: диапазон `int16` — до 32767.
* `category`: набор уникальных значений (`categories`) плюс целые коды. Выигрыш большой, когда уникальных значений мало относительно числа строк. Минусы: присвоить значение вне `categories` нельзя (будет ошибка, надо сначала расширить категории), а при группировке по нескольким категориальным ключам помните про параметр `observed`.
* Хранение строк: в этом окружении pyarrow не установлен, поэтому `str` использует хранение на Python-объектах; результаты измерений размера могут отличаться между окружениями. Поэтому проверяйте не абсолютные числа, а сравнения («стало меньше в разы»).

## Цепочки методов

`df.query(...)`, `.assign(новый=lambda d: ...)`, `.pipe(func)`, `.groupby(...).agg(...)`, `.sort_values(...)`, `.reset_index()` — каждый метод возвращает новый объект, исходный DataFrame не меняется (Copy-on-Write), а `lambda d:` в `assign` видит результат предыдущего шага. Цепочка проще читается и тестируется, чем десяток промежуточных переменных.

## Когда pandas недостаточно

pandas работает **в памяти одного процесса** и вычисляет операции **сразу** (eager). Если данные не помещаются в память или нужен оптимизатор запросов, смотрят на другие инструменты. Принцип, который стоит знать: **ленивые вычисления** (lazy) — вы описываете запрос целиком, а движок строит план, пропускает ненужные столбцы и строки и выполняет всё за один проход; **колоночное хранение** — данные лежат по столбцам, и запрос читает только нужные. Такой подход используют Polars (библиотека DataFrame с ленивым режимом) и DuckDB (встраиваемая аналитическая SQL-СУБД, читающая файлы напрямую). Здесь они описаны только как идея: в этом курсе они не запускаются, и сравнивать их скорость с pandas мы не будем. Официальные сайты: [pola.rs](https://pola.rs/) и [duckdb.org](https://duckdb.org/).

<!-- section: examples -->
## Примеры

**Пример 1. apply вызывает функцию на каждой строке.**

```python run
import numpy as np
import pandas as pd

df = pd.DataFrame({"load": np.arange(1000, dtype="float64"), "rate": 0.5})
calls = 0


def per_row(row):
    global calls
    calls += 1
    return row["load"] * row["rate"]


slow = df.apply(per_row, axis=1)
fast = df["load"] * df["rate"]
print("вызовов функции:", calls)
print("результаты равны:", np.allclose(slow, fast))
```
```output
вызовов функции: 1000
результаты равны: True
```

**Пример 2. Векторные замены условных циклов.**

```python run
import numpy as np
import pandas as pd

state = pd.Series(["ok", "warn", "fault", "ok"])
load = pd.Series([50.0, 40.0, 10.0, 100.0])
rate = state.map({"ok": 0.10, "warn": 0.15, "fault": 0.30})
print(rate.tolist())
print((load * rate).tolist())
cond = np.select([load >= 100, load >= 40], ["high", "mid"], default="low")
print(cond.tolist())
print(np.where(load > 45, "big", "small").tolist())
```
```output
[0.1, 0.15, 0.3, 0.1]
[5.0, 6.0, 3.0, 10.0]
['mid', 'mid', 'low', 'high']
['big', 'small', 'small', 'big']
```

**Пример 3. Размеры dtype и deep.**

```python run
import numpy as np
import pandas as pd

n = pd.Series(np.arange(1000))
print(n.dtype, n.memory_usage(index=False))
print(n.astype("int16").dtype, n.astype("int16").memory_usage(index=False))
print(pd.to_numeric(pd.Series([1, 2, 3]), downcast="integer").dtype)

words = pd.Series(["alpha"] * 1000)
shallow = words.memory_usage(index=False)
deep = words.memory_usage(index=False, deep=True)
print("deep больше shallow:", deep > shallow)
```
```output
int64 8000
int16 2000
int8
deep больше shallow: True
```

**Пример 4. category: меньше памяти, но есть ограничения.**

```python run
import pandas as pd

states = pd.Series(["ok", "warn", "ok", "ok", "fault"] * 200)
cat = states.astype("category")
print(cat.cat.categories.tolist(), cat.cat.codes.dtype)
print("более чем в 10 раз меньше:", cat.memory_usage(index=False, deep=True) * 10 < states.memory_usage(index=False, deep=True))
print((cat == "ok").sum(), states.value_counts().to_dict())
try:
    cat.iloc[0] = "unknown"
except TypeError as exc:
    print("TypeError:", str(exc)[:46])
```
```output
['fault', 'ok', 'warn'] int8
более чем в 10 раз меньше: True
600 {'ok': 600, 'warn': 200, 'fault': 200}
TypeError: Cannot setitem on a Categorical with a new cat
```

**Пример 5. Цепочка методов.**

```python run
import pandas as pd

pd.set_option("display.width", 80)
df = pd.DataFrame(
    {
        "site": ["a", "a", "b", "b", "b"],
        "state": ["ok", "fault", "ok", "ok", "warn"],
        "temp_c": [20.0, 90.0, 10.0, 30.0, 50.0],
    }
)
report = (
    df.query("state == 'ok'")
    .assign(temp_f=lambda d: d["temp_c"] * 9 / 5 + 32)
    .groupby("site")
    .agg(readings=("temp_f", "count"), mean_temp_f=("temp_f", "mean"))
    .reset_index()
)
print(report)
print(df.shape, "исходная таблица не изменилась")
```
```output
  site  readings  mean_temp_f
0    a         1         68.0
1    b         2         68.0
(5, 3) исходная таблица не изменилась
```

<!-- section: mistakes -->
## Типичные ошибки

* **`apply(axis=1)` и `iterrows` там, где есть векторное выражение.** Сначала `np.where`, `np.select`, `map`, арифметика столбцов.
* **Оптимизировать вслепую.** Сначала измерьте (`timeit`, `perf_counter`, `memory_usage(deep=True)`), потом меняйте.
* **Смотреть `memory_usage()` без `deep=True`.** Для строковых столбцов вы увидите только указатели и сильно недооцените размер.
* **Переводить в `category` столбец с почти уникальными значениями.** Выигрыша нет: словарь категорий почти равен самому столбцу.
* **Сужать целые без проверки диапазона.** Новые данные, вышедшие за диапазон `int16`, не поместятся; проверяйте границы заранее.
* **Присваивать новые значения в категориальный столбец без расширения категорий.** Получите `TypeError`.
* **Считать, что Polars или DuckDB «просто быстрее».** Это другой способ работы (lazy, колоночное хранение); выбор зависит от объёма данных и задачи, и его нужно проверять замером на своих данных.
* **Длинные цепочки без промежуточной проверки.** Если результат неверен, разбейте цепочку и посмотрите на `shape` после каждого шага.

<!-- section: summary -->
## Итоги

* Векторные операции над столбцами быстрее построчных: `apply(axis=1)` вызывает функцию Python на каждой строке.
* `memory_usage(deep=True)` показывает реальную память, `category` и узкие целые dtype её сокращают; проверяйте диапазон и долю уникальных значений.
* Цепочки `query → assign → groupby → agg` читаются как конвейер и не меняют вход.
* pandas вычисляет сразу и в памяти; Polars (lazy, колоночный) и DuckDB (SQL по файлам) решают те же задачи иначе, и выбор подтверждается замером.
* Измеряйте, а не предполагайте.

Это последний урок курса. Повторите [группировку](lesson:python.pandas.groupby) и [объединение таблиц](lesson:python.pandas.joins-and-reshaping); основа по типам — [dtype в NumPy](lesson:python.numpy.dtypes-and-casting).
