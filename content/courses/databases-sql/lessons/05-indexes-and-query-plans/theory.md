<!-- section: simple -->
## Индекс как оглавление

Как найти в книге все места, где упоминается слово «транзакция»? Можно прочитать книгу целиком. Или открыть алфавитный указатель в конце: там слова упорядочены, рядом с каждым записаны номера страниц. Указатель занимает место, его нужно обновлять при каждой правке книги, зато ищет мгновенно.

**Индекс** в базе данных - это такой указатель: отдельная структура (обычно B-дерево), в которой значения столбца упорядочены и привязаны к строкам таблицы. Без индекса запрос `WHERE customer_id = 5` вынужден просмотреть **каждую** строку таблицы (в плане запроса это `SCAN`). С индексом СУБД спускается по дереву к нужному значению за несколько шагов (`SEARCH`). Для таблицы в миллион строк разница - порядки величин.

Но индексы не бесплатны:

* каждый индекс занимает место;
* при каждой вставке, удалении и (для индексированных столбцов) обновлении строки нужно обновить и все индексы;
* лишний индекс, который никто не использует, - чистые потери.

Поэтому индексы создают **под конкретные запросы** и проверяют результат инструментом, который показывает, как база собирается выполнять запрос: `EXPLAIN QUERY PLAN`. Гадать не нужно: план говорит, использован ли индекс. Урок опирается на [схему и ключи](lesson:databases.sql.schema-design-and-normalization) и [соединения](lesson:databases.sql.joins): индексы по столбцам соединения - первое, что проверяют в медленном запросе.

<!-- section: precise -->
## Как читать план и выбирать индекс

**B-дерево.** Индекс хранит пары «значение ключа -> rowid» в упорядоченном дереве. Операции: найти значение, найти диапазон, обойти в порядке сортировки. Высота дерева растёт логарифмически, поэтому поиск по миллиарду строк занимает десятки обращений. Первичный ключ `INTEGER PRIMARY KEY` в SQLite - это и есть `rowid`, отдельный индекс для него не нужен (в плане: `SEARCH ... USING INTEGER PRIMARY KEY (rowid=?)`). Ограничения `UNIQUE` и `PRIMARY KEY` на других столбцах создают индекс автоматически.

**EXPLAIN QUERY PLAN.** `EXPLAIN QUERY PLAN <запрос>` возвращает строки; в Python последний столбец (`row[3]`, поле `detail`) содержит текст. Основные формы (проверено на SQLite 3.53.4; формат предназначен для человека и **может меняться** между версиями):

| текст плана | смысл |
|---|---|
| `SCAN orders` | чтение всей таблицы |
| `SEARCH orders USING INDEX idx (a=?)` | поиск по индексу, использованы условия на столбцы в скобках |
| `SEARCH orders USING COVERING INDEX idx (a=?)` | то же, но таблица не читается: в индексе есть все нужные столбцы |
| `SEARCH orders USING INTEGER PRIMARY KEY (rowid=?)` | поиск по первичному ключу |
| `SCAN orders USING INDEX idx` | обход таблицы в порядке индекса (например, для `ORDER BY`) |
| `USE TEMP B-TREE FOR ORDER BY` | сортировка во временной структуре, индекс порядок не обеспечил |

В скобках после имени индекса перечислены условия, по которым идёт поиск: `(a=? AND b>?)`. Если условие на столбец не попало в скобки, индекс его не использует для поиска.

**Составной индекс и правило левого префикса.** Индекс `(a, b, c)` упорядочен сначала по `a`, внутри равных `a` - по `b`, внутри равных `b` - по `c`. Поэтому он помогает запросам:

* по `a`; по `a` и `b`; по `a`, `b`, `c` (левые префиксы);
* по `a` с диапазоном по `b`: `a = ? AND b > ?`;
* для `ORDER BY a, b` и `WHERE a = ? ORDER BY b` (порядок уже есть).

И **не** помогает искать по `b` отдельно, по `c`, и по `b, c` (нет левого префикса). Правила выбора порядка: сначала столбцы с условием равенства, затем один столбец с диапазоном или сортировкой; после первого диапазонного столбца остальные столбцы индекса для поиска не используются. Отдельные индексы `(a)` и `(b)` не заменяют `(a, b)`: SQLite, как правило, использует один индекс на таблицу в запросе.

**Избирательность (selectivity)** - доля строк, которую отсекает условие. Индекс по столбцу, где почти все значения одинаковы (пол, булев флаг, статус из 3 значений), мало помогает: он возвращает большую часть таблицы, и чтение через индекс может оказаться дороже `SCAN`. Для оценки планировщик использует статистику: `ANALYZE` записывает её в `sqlite_stat1` (в примере 3 строка `20000 6667 239` для индекса `(status, created_at)` означает: 20000 строк, в среднем 6667 строк на значение `status` и 239 на пару `(status, created_at)`). Высокоизбирательные столбцы (id клиента, email) выгодны сразу. Составной индекс может быть избирательным, даже если каждый его столбец по отдельности нет.

**Покрывающий индекс.** Если все столбцы, нужные запросу (из `SELECT` и `WHERE`), есть в индексе, строки таблицы не читаются: `USING COVERING INDEX`. Платой становится размер индекса. Добавляйте столбцы `SELECT` в конец индекса, не раньше условных столбцов.

**Когда индекс не используется.**

* Функция или выражение над столбцом: `WHERE lower(status) = 'paid'` не использует индекс по `status`. Решение: хранить значение уже нормализованным или создать индекс по выражению (`CREATE INDEX ... ON t(lower(status))`, SQLite ≥ 3.9.0).
* Условие не по левому префиксу составного индекса.
* Низкая избирательность: планировщик может предпочесть `SCAN`.
* `LIKE '%abc'` (шаблон начинается с подстановки).

**Цена записи.** Вставка строки в таблицу с `k` индексами выполняет `1 + k` записей в B-деревья. Для таблиц с интенсивной записью и редким чтением лишние индексы вредны. Чем шире индекс, тем больше он весит. *Частичные индексы* (`CREATE INDEX ... WHERE status = 'new'`, SQLite ≥ 3.8.0) хранят только нужную часть строк.

**Другие СУБД.** В PostgreSQL план смотрят через `EXPLAIN` и `EXPLAIN ANALYZE` (реальное время и число строк), узлы называются `Seq Scan`, `Index Scan`, `Index Only Scan`; индексы бывают не только B-tree (GIN для массивов и JSONB, GiST, BRIN). В MySQL (InnoDB) первичный ключ является кластерным индексом (данные хранятся в порядке ключа), а вторичные индексы содержат значение первичного ключа.

<!-- section: examples -->
## Примеры

**Пример 1. От SCAN к SEARCH.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.execute("CREATE TABLE orders(id INTEGER PRIMARY KEY, customer_id INTEGER NOT NULL, status TEXT NOT NULL, created_at TEXT NOT NULL, total INTEGER NOT NULL, note TEXT)")
statuses = ["new", "paid", "shipped", "done", "cancelled"]
conn.executemany(
    "INSERT INTO orders(customer_id, status, created_at, total, note) VALUES (?, ?, ?, ?, ?)",
    [(i % 200, statuses[i % 5], "2024-%02d-%02d" % (i % 12 + 1, i % 28 + 1), i % 977, "n%d" % i) for i in range(3000)],
)

def plan(sql, params=()):
    return [row[3] for row in conn.execute("EXPLAIN QUERY PLAN " + sql, params)]

q = "SELECT * FROM orders WHERE customer_id = ?"
print("без индекса :", plan(q, (5,)))
conn.execute("CREATE INDEX idx_customer ON orders(customer_id)")
print("с индексом  :", plan(q, (5,)))
print("по первичному ключу:", plan("SELECT * FROM orders WHERE id = ?", (5,)))
print("число строк результата:", len(conn.execute(q, (5,)).fetchall()))
print("не по префиксу:", plan("SELECT * FROM orders WHERE total = ?", (5,)))
```
```output
без индекса : ['SCAN orders']
с индексом  : ['SEARCH orders USING INDEX idx_customer (customer_id=?)']
по первичному ключу: ['SEARCH orders USING INTEGER PRIMARY KEY (rowid=?)']
число строк результата: 15
не по префиксу: ['SCAN orders']
```

**Пример 2. Составной индекс: левый префикс, диапазон и сортировка.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.execute("CREATE TABLE orders(id INTEGER PRIMARY KEY, customer_id INTEGER NOT NULL, status TEXT NOT NULL, created_at TEXT NOT NULL, total INTEGER NOT NULL)")
conn.executemany(
    "INSERT INTO orders(customer_id, status, created_at, total) VALUES (?, ?, ?, ?)",
    [(i % 200, ["new", "paid", "done"][i % 3], "2024-%02d-%02d" % (i % 12 + 1, i % 28 + 1), i % 977) for i in range(3000)],
)
conn.execute("CREATE INDEX idx_cs ON orders(customer_id, status, created_at)")

def plan(sql, params=()):
    return [row[3] for row in conn.execute("EXPLAIN QUERY PLAN " + sql, params)]

cases = [
    ("a", "SELECT * FROM orders WHERE customer_id = 1"),
    ("a, b", "SELECT * FROM orders WHERE customer_id = 1 AND status = 'paid'"),
    ("a, b, диапазон c", "SELECT * FROM orders WHERE customer_id = 1 AND status = 'paid' AND created_at > '2024-06-01'"),
    ("только b", "SELECT * FROM orders WHERE status = 'paid'"),
    ("a и b-диапазон, потом c", "SELECT * FROM orders WHERE customer_id = 1 AND status > 'm' AND created_at > '2024-06-01'"),
    ("a + ORDER BY b", "SELECT * FROM orders WHERE customer_id = 1 ORDER BY status"),
    ("ORDER BY created_at", "SELECT * FROM orders ORDER BY created_at"),
]
for label, sql in cases:
    print(f"{label:26}", plan(sql))
```
```output
a                          ['SEARCH orders USING INDEX idx_cs (customer_id=?)']
a, b                       ['SEARCH orders USING INDEX idx_cs (customer_id=? AND status=?)']
a, b, диапазон c           ['SEARCH orders USING INDEX idx_cs (customer_id=? AND status=? AND created_at>?)']
только b                   ['SCAN orders']
a и b-диапазон, потом c    ['SEARCH orders USING INDEX idx_cs (customer_id=? AND status>?)']
a + ORDER BY b             ['SEARCH orders USING INDEX idx_cs (customer_id=?)']
ORDER BY created_at        ['SCAN orders', 'USE TEMP B-TREE FOR ORDER BY']
```

В запросе «только b» индекс не используется: `status` не левый префикс. Диапазон по `status` останавливает использование следующего столбца: `created_at` в скобки не попал. Запрос `a + ORDER BY b` обошёлся без `USE TEMP B-TREE`, потому что внутри одного `customer_id` значения `status` в индексе уже идут по порядку.

**Пример 3. Покрывающий индекс, функция над столбцом, статистика и цена записи.**

```python run
import os
import sqlite3
import tempfile

def build(path, with_indexes):
    conn = sqlite3.connect(path)
    conn.execute("CREATE TABLE orders(id INTEGER PRIMARY KEY, customer_id INTEGER NOT NULL, status TEXT NOT NULL, created_at TEXT NOT NULL, total INTEGER NOT NULL)")
    if with_indexes:
        conn.execute("CREATE INDEX i1 ON orders(customer_id, total)")
        conn.execute("CREATE INDEX i2 ON orders(status, created_at)")
        conn.execute("CREATE INDEX i3 ON orders(created_at, customer_id)")
    conn.executemany(
        "INSERT INTO orders(customer_id, status, created_at, total) VALUES (?, ?, ?, ?)",
        [(i % 200, ["new", "paid", "done"][i % 3], "2024-%02d-%02d" % (i % 12 + 1, i % 28 + 1), i % 977) for i in range(20000)],
    )
    conn.commit()
    return conn

def plan(conn, sql, params=()):
    return [row[3] for row in conn.execute("EXPLAIN QUERY PLAN " + sql, params)]

with tempfile.TemporaryDirectory() as tmp:
    plain = build(os.path.join(tmp, "plain.db"), False)
    indexed = build(os.path.join(tmp, "indexed.db"), True)

    print(plan(indexed, "SELECT customer_id, total FROM orders WHERE customer_id = ?", (5,)))
    print(plan(indexed, "SELECT * FROM orders WHERE customer_id = ?", (5,)))
    print("функция над столбцом:", plan(indexed, "SELECT * FROM orders WHERE upper(status) = 'PAID'"))
    indexed.execute("CREATE INDEX i4 ON orders(upper(status))")
    print("индекс по выражению :", plan(indexed, "SELECT * FROM orders WHERE upper(status) = 'PAID'"))

    indexed.execute("ANALYZE")
    print("sqlite_stat1:", indexed.execute("SELECT idx, stat FROM sqlite_stat1 WHERE idx IN ('i1', 'i2') ORDER BY idx").fetchall())

    size_plain = os.path.getsize(os.path.join(tmp, "plain.db"))
    size_indexed = os.path.getsize(os.path.join(tmp, "indexed.db"))
    print("файл с индексами больше:", size_indexed > size_plain)
    plain.close()
    indexed.close()
```
```output
['SEARCH orders USING COVERING INDEX i1 (customer_id=?)']
['SEARCH orders USING INDEX i1 (customer_id=?)']
функция над столбцом: ['SCAN orders']
индекс по выражению : ['SEARCH orders USING INDEX i4 (<expr>=?)']
sqlite_stat1: [('i1', '20000 100 1'), ('i2', '20000 6667 239')]
файл с индексами больше: True
```

Первый запрос читает только индекс (`COVERING INDEX`), второй вынужден заглядывать в таблицу за остальными столбцами. Файл с индексами заметно больше, и каждая вставка в него выполнила ещё три записи в деревья.

<!-- section: mistakes -->
## Типичные ошибки

* **Индекс на каждый столбец.** Замедляет запись, занимает место и часто не используется. Создавайте индексы под конкретные запросы из плана.
* **Неверный порядок столбцов.** `(created_at, status)` не обслуживает `WHERE status = ? AND created_at >= ?` так, как `(status, created_at)`. Равенства - слева, диапазон - последним.
* **Ожидать, что `(a, b)` ускорит запрос по `b`.** Нужен индекс, начинающийся с `b`, либо условие на `a`.
* **Функция над индексированным столбцом** (`lower(email) = ...`, `substr(...)`, `date(created_at)`). Индекс не применяется; перепишите условие как диапазон по столбцу или создайте индекс по выражению.
* **Верить предположениям вместо плана.** Проверяйте `EXPLAIN QUERY PLAN` на реалистичных данных: на пустой или крошечной таблице план может отличаться.
* **Индекс на столбце с низкой избирательностью.** Двух-трёх значений мало; такой индекс полезен только в составе других или как частичный.
* **Считать текст плана стабильным API.** Формат `EXPLAIN QUERY PLAN` может меняться между версиями SQLite; в проверках сравнивайте только устойчивые части и фиксируйте версию.
* **Забыть про индексы по внешним ключам.** Столбец `orders.customer_id` в соединениях и при каскадном удалении родителя ищут по значению: без индекса это `SCAN` на каждую операцию.
* **Перегрузить таблицу с интенсивной записью.** Для журналов и потоков событий минимизируйте число индексов.
* **Удалять и пересоздавать индексы на живой системе без измерений.** Сначала план и нагрузочный тест, потом изменение схемы.

<!-- section: summary -->
## Итог

* Индекс - упорядоченная структура (B-tree), которая заменяет полный просмотр таблицы (`SCAN`) поиском (`SEARCH`); стоимость - место и замедление записи.
* `EXPLAIN QUERY PLAN` показывает использованный индекс, условия поиска и покрывающий ли он; формат может меняться между версиями (здесь проверено на SQLite 3.53.4).
* Составной индекс работает по левому префиксу: сначала равенства, затем один диапазон или сортировка; запрос только по второму столбцу индекса его не использует.
* Покрывающий индекс содержит все читаемые столбцы и избавляет от обращения к таблице.
* Функция над столбцом блокирует индекс (нужен индекс по выражению); избирательность и статистика (`ANALYZE`, `sqlite_stat1`) влияют на выбор плана.
* Дальше: [транзакции и изоляция](lesson:databases.sql.transactions-and-isolation) - как записи сохраняются целиком или не сохраняются совсем.
