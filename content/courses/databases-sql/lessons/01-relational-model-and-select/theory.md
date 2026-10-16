<!-- section: simple -->
## Данные как таблицы, вопросы как запросы

Реляционная база хранит данные в **таблицах**. Таблица - это набор однотипных записей: у каждой записи (строки) одни и те же столбцы, и у каждого столбца свой тип. Список сотрудников, заказов, товаров - всё это таблицы. Вы не говорите базе, *как* искать, вы описываете, *какие* строки нужны, на языке SQL (Structured Query Language). Как именно выполнить запрос, решает сама СУБД.

Три идеи, на которых держится всё остальное:

* **Строка идентифицируется ключом.** Столбец (или набор столбцов), значения которого однозначно определяют строку, называется *первичным ключом* (PRIMARY KEY). Строка другой таблицы ссылается на неё значением этого ключа - это *внешний ключ*. Так связываются «заказ» и «клиент», не копируя данные клиента в каждый заказ.
* **Строки не упорядочены.** Таблица - это множество, а не список. Если вам нужен порядок, его нужно запросить явно через `ORDER BY`.
* **Значения может не быть.** Для «неизвестно» в SQL есть специальное значение `NULL`. Это не ноль и не пустая строка, и поэтому оно ведёт себя особым образом: любое сравнение с ним даёт не «да» и не «нет», а «неизвестно». Из-за этого в запросах появляются самые неожиданные ошибки, и большая часть урока посвящена именно им.

В курсе теория не привязана к языку: SQL один и тот же. Упражнения выполняются на Python через встроенный модуль `sqlite3` - SQLite хранит базу в файле или в памяти и не требует сервера. Где PostgreSQL или MySQL ведут себя иначе, это указано отдельно. Основы Python, которые здесь нужны - [функции](lesson:python.fundamentals.functions) и [обработка ошибок](lesson:python.fundamentals.errors-and-files).

Минимальный запрос выглядит так:

```sql
SELECT name, salary      -- какие столбцы показать
FROM employees           -- из какой таблицы
WHERE salary > 100000    -- какие строки оставить
ORDER BY salary DESC     -- в каком порядке
LIMIT 3;                 -- сколько строк вернуть
```

<!-- section: precise -->
## Точные правила

**Таблицы, типы и ключи.** `CREATE TABLE` задаёт имена столбцов, типы и ограничения. `PRIMARY KEY` гарантирует уникальность и (в стандарте) отсутствие `NULL`. В SQLite столбец `INTEGER PRIMARY KEY` является псевдонимом внутреннего номера строки `rowid` и при вставке без значения получает следующее число автоматически. Типы в SQLite работают иначе, чем в PostgreSQL: у столбца есть *сродство типа* (type affinity), и значение другого типа обычно сохраняется как есть (в столбец `INTEGER` можно записать строку `'abc'`). В PostgreSQL и MySQL тип строгий, неподходящее значение вызывает ошибку. SQLite ≥ 3.37 поддерживает строгие таблицы: `CREATE TABLE ... STRICT` проверяет типы (допустимые типы - `INT`, `INTEGER`, `REAL`, `TEXT`, `BLOB`, `ANY`). Подробнее - в [уроке о схеме](lesson:databases.sql.schema-design-and-normalization).

**Главные операторы SELECT.**

* *Проекция*: список столбцов и выражений; `AS` задаёт псевдоним; `*` - все столбцы (в коде приложения лучше перечислять нужные).
* *Фильтр* `WHERE`: условие по строке. Операторы `=`, `<>` (или `!=`), `<`, `<=`, `>`, `>=`, `BETWEEN a AND b` (границы включены), `IN (...)`, `LIKE` (шаблоны `%` и `_`), логические `AND`, `OR`, `NOT`.
* *Уникальность* `DISTINCT` убирает повторяющиеся строки результата целиком.
* *Сортировка* `ORDER BY a ASC, b DESC`: несколько ключей, по умолчанию `ASC`. Если нужно детерминированное упорядочение, добавляйте в конец уникальный ключ.
* *Ограничение* `LIMIT n OFFSET k`: пропустить `k` строк и вернуть не более `n`. Без `ORDER BY` результат «страницы» не определён.

**Трёхзначная логика.** В SQL у условия три значения: *true*, *false* и *unknown* (NULL). Правила:

| выражение | результат |
|---|---|
| `NULL = NULL`, `NULL <> 1`, `x > NULL` | NULL |
| `NULL AND false` | false |
| `NULL AND true` | NULL |
| `NULL OR true` | true |
| `NULL OR false` | NULL |
| `NOT NULL` | NULL |

`WHERE` оставляет строку **только если условие true**; false и NULL отбрасывают её. Поэтому `x <> 1` не возвращает строки, где `x IS NULL`. Проверка на отсутствие значения - `IS NULL` / `IS NOT NULL`. Функция `COALESCE(a, b, ...)` возвращает первый аргумент, не равный NULL. Арифметика и склейка строк с NULL дают NULL (`NULL * 12`, `'a' || NULL`). В SQLite есть также операторы `IS` и `IS NOT`, которые сравнивают, считая NULL обычным значением (`NULL IS NULL` истинно, `1 IS NOT NULL` истинно); в PostgreSQL аналог - `IS NOT DISTINCT FROM`, в MySQL - `<=>`. `DISTINCT` и `GROUP BY` считают все NULL одинаковыми, а `UNIQUE`-ограничение в SQLite и PostgreSQL допускает несколько NULL.

**Порядок логической обработки.** Запрос пишется в одном порядке, а *логически* выполняется в другом:

1. `FROM` (и соединения) - откуда берутся строки;
2. `WHERE` - фильтр строк;
3. `GROUP BY` и агрегаты, затем `HAVING` - фильтр групп (следующие уроки);
4. оконные функции;
5. `SELECT` - вычисление списка выражений и псевдонимов, затем `DISTINCT`;
6. `ORDER BY` - сортировка (здесь уже видны псевдонимы из `SELECT`);
7. `LIMIT` / `OFFSET`.

Физически СУБД вправе переставлять шаги, лишь бы результат совпал. Следствие: псевдоним, заданный в `SELECT`, по стандарту нельзя использовать в `WHERE`, но можно в `ORDER BY`. SQLite исключение допускает, PostgreSQL - нет.

**Python и sqlite3.** `sqlite3.connect(":memory:")` создаёт базу в памяти. `conn.execute(sql, params)` возвращает курсор; `fetchall()` - список кортежей; `executemany` вставляет пачку строк. Значения всегда передают параметрами `?` или `:name` (подробнее в [уроке о безопасном доступе](lesson:databases.sql.sql-injection-and-safe-access)). Проверено на SQLite 3.53.4.

<!-- section: examples -->
## Примеры

**Пример 1. Таблица, SELECT, WHERE, ORDER BY, LIMIT.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.execute("CREATE TABLE employees(id INTEGER PRIMARY KEY, name TEXT NOT NULL, dept TEXT, salary INTEGER)")
conn.executemany(
    "INSERT INTO employees(name, dept, salary) VALUES (?, ?, ?)",
    [("Anna", "Dev", 150000), ("Boris", "Dev", 120000), ("Vera", "Sales", 90000),
     ("Gleb", "Sales", None), ("Dina", None, 100000)],
)

rows = conn.execute(
    "SELECT name, salary FROM employees WHERE salary >= ? ORDER BY salary DESC, name LIMIT 3", (100000,)
).fetchall()
print(rows)
print(conn.execute("SELECT id, name FROM employees ORDER BY id").fetchall())
print(conn.execute("SELECT DISTINCT dept FROM employees ORDER BY dept").fetchall())
```
```output
[('Anna', 150000), ('Boris', 120000), ('Dina', 100000)]
[(1, 'Anna'), (2, 'Boris'), (3, 'Vera'), (4, 'Gleb'), (5, 'Dina')]
[(None,), ('Dev',), ('Sales',)]
```

В последнем запросе `NULL` попал в результат и стоит первым: при `ORDER BY ... ASC` SQLite считает `NULL` меньшим любого значения (PostgreSQL по умолчанию ставит его в конец).

**Пример 2. Трёхзначная логика на практике.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
print(conn.execute("SELECT NULL = NULL, NULL IS NULL, NULL AND 0, NULL OR 1, NOT NULL, 1 + NULL").fetchone())

conn.execute("CREATE TABLE t(x INTEGER)")
conn.executemany("INSERT INTO t VALUES (?)", [(1,), (2,), (None,)])
for cond in ["x <> 1", "NOT (x = 1)", "x <> 1 OR x IS NULL", "x IS NOT 1", "x = NULL", "x IS NULL"]:
    rows = conn.execute(f"SELECT x FROM t WHERE {cond} ORDER BY x").fetchall()
    print(f"{cond:22} -> {rows}")
print(conn.execute("SELECT COUNT(*), COUNT(x) FROM t").fetchone())
```
```output
(None, 1, 0, 1, None, None)
x <> 1                 -> [(2,)]
NOT (x = 1)            -> [(2,)]
x <> 1 OR x IS NULL    -> [(None,), (2,)]
x IS NOT 1             -> [(None,), (2,)]
x = NULL               -> []
x IS NULL              -> [(None,)]
(3, 2)
```

(Здесь условие подставлено в f-строку только потому, что это константы нашего собственного кода; значения от пользователя так подставлять нельзя - см. последний урок.)

**Пример 3. Порядок обработки: псевдоним, COALESCE, постраничный вывод.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.execute("CREATE TABLE e(id INTEGER PRIMARY KEY, name TEXT, salary INTEGER)")
conn.executemany("INSERT INTO e(name, salary) VALUES (?, ?)", [("a", 10), ("b", None), ("c", 30), ("d", 20)])

print(conn.execute("SELECT name, COALESCE(salary, 0) * 12 AS annual FROM e ORDER BY annual DESC, name").fetchall())
for page in (1, 2):
    size = 2
    rows = conn.execute("SELECT id, name FROM e ORDER BY id LIMIT ? OFFSET ?", (size, (page - 1) * size)).fetchall()
    print("страница", page, rows)
```
```output
[('c', 360), ('d', 240), ('a', 120), ('b', 0)]
страница 1 [(1, 'a'), (2, 'b')]
страница 2 [(3, 'c'), (4, 'd')]
```

Псевдоним `annual` доступен в `ORDER BY`, потому что сортировка выполняется после `SELECT`. Страницы получаются согласованными только благодаря `ORDER BY id`.

<!-- section: mistakes -->
## Типичные ошибки

* **`WHERE x = NULL`.** Всегда NULL, ни одна строка не вернётся. Пишите `x IS NULL`.
* **`WHERE dept <> 'Sales'` и «пропавшие» строки.** Строки с `dept IS NULL` исключаются. Добавьте `OR dept IS NULL` или используйте `dept IS NOT 'Sales'` (только SQLite).
* **Сортировка с NULL.** В SQLite `NULL` идёт первым при `ASC`, в PostgreSQL - последним. Для переносимости отфильтруйте их или управляйте явно (`ORDER BY x IS NULL, x`; `NULLS LAST` есть в PostgreSQL и в SQLite с 3.30.0).
* **Положиться на порядок без `ORDER BY`.** Сегодня строки приходят в порядке вставки, завтра после индекса или обновления - в другом. Для постраничного вывода нужен уникальный ключ сортировки, иначе строки повторяются или теряются между страницами.
* **Большой `OFFSET`.** `OFFSET 100000` всё равно вычитывает и отбрасывает 100000 строк. Для длинных списков используют поиск по ключу (`WHERE id > :last_id ORDER BY id LIMIT 50`).
* **`SELECT *` в коде приложения.** Порядок и состав столбцов могут измениться, лишние данные передаются по сети. Перечисляйте столбцы.
* **Псевдоним в `WHERE`.** Работает в SQLite, но ломается при переходе на PostgreSQL. Повторите выражение или используйте подзапрос.
* **Считать `NULL` нулём или пустой строкой.** `NULL + 1` - это NULL, `'a' || NULL` - NULL. Для значения по умолчанию есть `COALESCE`.
* **Целочисленное деление.** В SQLite `7 / 2` равно `3`, а `7.0 / 2` - `3.5` (PostgreSQL ведёт себя так же для целых типов).

<!-- section: summary -->
## Итог

* Таблица - неупорядоченное множество строк; первичный ключ идентифицирует строку, внешний ссылается на чужой первичный ключ.
* `SELECT ... FROM ... WHERE ... ORDER BY ... LIMIT` - основа запросов; логический порядок: FROM, WHERE, GROUP BY, HAVING, SELECT, ORDER BY, LIMIT.
* В SQL три значения истинности. `WHERE` пропускает только true, поэтому любое сравнение с NULL строку отбрасывает; проверяйте `IS NULL` / `IS NOT NULL`, подставляйте значение через `COALESCE`.
* Порядок гарантируется только `ORDER BY`; для страниц добавляйте уникальный ключ и по возможности используйте поиск по ключу вместо большого `OFFSET`.
* Типы в SQLite - сродство, а не строгая проверка (кроме `STRICT`-таблиц); в PostgreSQL и MySQL типы строгие.
* Дальше: [соединения таблиц](lesson:databases.sql.joins) - как собирать данные из нескольких таблиц и не размножить строки.
