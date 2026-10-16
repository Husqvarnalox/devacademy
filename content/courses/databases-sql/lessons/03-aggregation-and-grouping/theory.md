<!-- section: simple -->
## От строк к ответам

Запрос из прошлых уроков возвращал строки. Но часто нужен не список, а число или сводка: сколько заказов, какая средняя зарплата по отделам, кто лучший в каждой группе. Для этого есть два инструмента:

* **Агрегирование (`GROUP BY`)** сворачивает много строк в одну. Разложите чеки по стопкам «по магазинам» и в каждой стопке посчитайте сумму - получится одна строка на магазин. Исходных чеков в результате уже не видно.
* **Оконные функции (`OVER`)** считают то же самое, но ничего не сворачивают. Каждая исходная строка остаётся, а рядом появляется вычисленное значение: номер строки в своей группе, нарастающий итог, сумма по группе. Это «взгляд через окно» на соседние строки.

Две вещи, которые путают чаще всего:

* *`WHERE` и `HAVING`.* `WHERE` отбирает **строки** до группировки, `HAVING` отбирает **группы** после неё.
* *`COUNT(*)` и `COUNT(столбец)`.* Первая считает строки, вторая - только строки, где столбец известен (не `NULL`).

Урок опирается на [NULL и порядок обработки запроса](lesson:databases.sql.relational-model-and-select) и на [соединения](lesson:databases.sql.joins): именно после JOIN чаще всего приходится сворачивать размноженные строки.

<!-- section: precise -->
## Агрегаты, GROUP BY, HAVING и окна

**Агрегатные функции.** `COUNT(*)` - число строк; `COUNT(x)` - число строк, где `x` не NULL; `COUNT(DISTINCT x)` - число различных известных значений; `SUM(x)`, `AVG(x)`, `MIN(x)`, `MAX(x)` игнорируют NULL. Если подходящих значений нет вообще (пустая таблица или все NULL), `SUM`, `AVG`, `MIN`, `MAX` возвращают `NULL`, а `COUNT` - `0`. В SQLite есть дополнительная `TOTAL(x)` - сумма как число с плавающей точкой, которая для пустого набора даёт `0.0`. Агрегат без `GROUP BY` образует одну группу из всех строк, поэтому запрос возвращает **ровно одну строку**, даже если таблица пуста. `AVG` целых чисел в SQLite возвращает вещественное число. `SUM` целых возвращает целое; при переполнении SQLite сообщает об ошибке, PostgreSQL расширяет тип.

**GROUP BY.** Строки с одинаковыми значениями выражений в `GROUP BY` образуют группу; все NULL считаются одной группой. В списке `SELECT` допустимы выражения из `GROUP BY` и агрегаты. SQLite нарушение этого правила допускает («bare columns» - значение берётся из произвольной строки группы), а PostgreSQL и MySQL (в режиме `ONLY_FULL_GROUP_BY`, включён по умолчанию) отвергают запрос. Пишите так, чтобы он выполнился везде.

**WHERE и HAVING.** Логический порядок: `FROM/JOIN` -> `WHERE` -> `GROUP BY` -> агрегаты -> `HAVING` -> `SELECT` -> `ORDER BY` -> `LIMIT`. Поэтому в `WHERE` нельзя использовать агрегаты, а `HAVING` видит их результат. Всё, что можно отфильтровать до группировки (`dept IS NOT NULL`, `year = 2024`), помещайте в `WHERE`: так сворачивается меньше строк, и условие может воспользоваться индексом.

**Оконные функции** (SQLite ≥ 3.25.0; PostgreSQL и MySQL 8.0 поддерживают их тоже):

```sql
функция(...) OVER (
    PARTITION BY dept        -- разбиение на независимые группы (необязательно)
    ORDER BY salary DESC, id -- порядок внутри группы (нужен для нумерации и нарастающих итогов)
    ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW  -- рамка (необязательно)
)
```

* Ранжирование: `ROW_NUMBER()` (уникальные номера 1, 2, 3...), `RANK()` (равные получают один номер, следующий пропускается), `DENSE_RANK()` (без пропусков).
* Смещение: `LAG(x)` и `LEAD(x)` - значение предыдущей и следующей строки.
* Агрегаты как оконные: `SUM(x) OVER (...)`, `AVG`, `COUNT`, `MIN`, `MAX`.
* Рамка по умолчанию при наличии `ORDER BY`: `RANGE BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW` - в неё входят все *равные* строки (peers). Нарастающая сумма по неуникальному ключу поэтому одинакова у равных строк. Для построчного итога укажите `ROWS` или добавьте уникальный ключ в `ORDER BY`. Без `ORDER BY` рамка - весь раздел.
* Оконные функции вычисляются после `WHERE`, `GROUP BY` и `HAVING` и **не могут стоять в `WHERE`**. Чтобы отфильтровать по номеру строки («топ-1 в группе»), вычислите окно во вложенном запросе или `WITH`, а условие запишите во внешнем.

Если у `ROW_NUMBER` не уникальный ключ сортировки, порядок равных строк произволен, и результат может меняться между запусками; добавляйте уникальный столбец. Все примеры ниже детерминированы благодаря `ORDER BY` по каждому выводимому столбцу.

<!-- section: examples -->
## Примеры

**Пример 1. GROUP BY, WHERE и HAVING, три вида COUNT.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.execute("CREATE TABLE sales(id INTEGER PRIMARY KEY, shop TEXT, amount INTEGER, year INTEGER)")
conn.executemany("INSERT INTO sales(shop, amount, year) VALUES (?, ?, ?)", [
    ("north", 100, 2023), ("north", 50, 2024), ("north", None, 2024),
    ("south", 70, 2024), ("south", 30, 2024), ("east", 20, 2023),
])

print(conn.execute("SELECT COUNT(*), COUNT(amount), COUNT(DISTINCT shop), SUM(amount), AVG(amount) FROM sales").fetchone())
print(conn.execute("SELECT shop, COUNT(*), SUM(amount) FROM sales GROUP BY shop ORDER BY shop").fetchall())
# WHERE отбирает строки ДО группировки, HAVING - группы ПОСЛЕ
print(conn.execute("SELECT shop, SUM(amount) FROM sales WHERE year = 2024 GROUP BY shop ORDER BY shop").fetchall())
print(conn.execute("SELECT shop, SUM(amount) AS s FROM sales GROUP BY shop HAVING SUM(amount) >= 100 ORDER BY shop").fetchall())
print(conn.execute("SELECT SUM(amount), COUNT(*), TOTAL(amount) FROM sales WHERE year = 1999").fetchone())
```
```output
(6, 5, 3, 270, 54.0)
[('east', 1, 20), ('north', 3, 150), ('south', 2, 100)]
[('north', 50), ('south', 100)]
[('north', 150), ('south', 100)]
(None, 0, 0.0)
```

В последней строке показано поведение пустого набора: `SUM` равна `None` (NULL), `COUNT(*)` равна 0, `TOTAL` равна 0.0. У группы `north` в первом запросе три строки, но `SUM` учитывает только две суммы.

**Пример 2. Оконные функции: ROW_NUMBER, сумма по разделу, нарастающий итог.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.execute("CREATE TABLE emp(id INTEGER PRIMARY KEY, name TEXT, dept TEXT, salary INTEGER)")
conn.executemany("INSERT INTO emp(name, dept, salary) VALUES (?, ?, ?)", [
    ("Anna", "Dev", 150), ("Boris", "Dev", 120), ("Zhanna", "Dev", 120),
    ("Vera", "Sales", 90), ("Gleb", "Sales", 80), ("Egor", "Ops", 70),
])

sql = """
SELECT name, dept, salary,
       ROW_NUMBER() OVER (PARTITION BY dept ORDER BY salary DESC, id) AS rn,
       SUM(salary)  OVER (PARTITION BY dept)                          AS dept_total,
       ROUND(100.0 * salary / SUM(salary) OVER (PARTITION BY dept), 1) AS pct
FROM emp
ORDER BY dept, rn
"""
for row in conn.execute(sql):
    print(row)

# Топ-1 в каждой группе: окно считаем во вложенном запросе, фильтруем снаружи
top = "SELECT dept, name FROM (SELECT dept, name, ROW_NUMBER() OVER (PARTITION BY dept ORDER BY salary DESC, id) AS rn FROM emp) WHERE rn = 1 ORDER BY dept"
print(conn.execute(top).fetchall())
```
```output
('Anna', 'Dev', 150, 1, 390, 38.5)
('Boris', 'Dev', 120, 2, 390, 30.8)
('Zhanna', 'Dev', 120, 3, 390, 30.8)
('Egor', 'Ops', 70, 1, 70, 100.0)
('Vera', 'Sales', 90, 1, 170, 52.9)
('Gleb', 'Sales', 80, 2, 170, 47.1)
[('Dev', 'Anna'), ('Ops', 'Egor'), ('Sales', 'Vera')]
```

Каждый сотрудник остался в результате, а к строке добавились номер в отделе, сумма отдела и доля - это то, чего `GROUP BY` дать не может.

**Пример 3. Рамка по умолчанию и равные строки.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.execute("CREATE TABLE p(id INTEGER PRIMARY KEY, x INTEGER)")
conn.executemany("INSERT INTO p(x) VALUES (?)", [(10,), (20,), (20,), (30,)])

sql = """
SELECT id, x,
       SUM(x) OVER (ORDER BY x)                                                AS by_default_frame,
       SUM(x) OVER (ORDER BY x, id ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS by_rows,
       RANK()       OVER (ORDER BY x) AS rnk,
       DENSE_RANK() OVER (ORDER BY x) AS dense,
       LAG(x)       OVER (ORDER BY id) AS prev
FROM p ORDER BY id
"""
print("id x  по умолчанию  ROWS  rank  dense  prev")
for row in conn.execute(sql):
    print(row)
```
```output
id x  по умолчанию  ROWS  rank  dense  prev
(1, 10, 10, 10, 1, 1, None)
(2, 20, 50, 30, 2, 2, 10)
(3, 20, 50, 50, 2, 2, 20)
(4, 30, 80, 80, 4, 3, 20)
```

Строки с `x = 20` в рамке по умолчанию получают общий итог `50`, потому что рамка включает обе равные строки сразу (10 + 20 + 20 = 50). С `ROWS` итог строится строго построчно: `10`, `30`, `50`, `80`.

<!-- section: mistakes -->
## Типичные ошибки

* **Агрегат в `WHERE`.** `WHERE COUNT(*) > 1` - ошибка. Используйте `HAVING`.
* **Условие на строки в `HAVING`.** `HAVING year = 2024` работает в SQLite, но выполняется после группировки и не использует индексы; в PostgreSQL это ошибка, если столбец не сгруппирован. Фильтры по строкам - в `WHERE`.
* **`COUNT(x)` вместо `COUNT(*)`.** Если в `x` есть NULL, строки «потеряются» из подсчёта. Считайте именно то, что хотели: строки или известные значения.
* **Считать `AVG` по данным с NULL как по всем строкам.** `AVG(x)` делит на число известных значений. Если неизвестное нужно считать нулём, оберните: `AVG(COALESCE(x, 0))`.
* **Негруппируемый столбец в `SELECT`.** SQLite вернёт значение из произвольной строки, PostgreSQL отвергнет запрос. Добавьте столбец в `GROUP BY` или используйте оконную функцию.
* **Суммы после JOIN.** Размноженные строки завышают `SUM` и `COUNT`; агрегируйте до соединения или считайте `COUNT(DISTINCT id)` (см. [соединения](lesson:databases.sql.joins)).
* **Оконная функция в `WHERE`.** Оконные функции вычисляются после `WHERE`; используйте вложенный запрос.
* **Недетерминированный `ROW_NUMBER`.** Без уникального ключа в `ORDER BY` равные строки получают номера в произвольном порядке.
* **Нарастающий итог по неуникальному ключу.** Рамка по умолчанию объединяет равные строки; для построчного итога укажите `ROWS` и уникальный ключ.
* **Использование оконных функций в старой сборке SQLite.** Нужна версия не ниже 3.25.0: проверьте `sqlite3.sqlite_version_info`.

<!-- section: summary -->
## Итог

* Агрегаты (`COUNT`, `SUM`, `AVG`, `MIN`, `MAX`) игнорируют NULL; `COUNT(*)` считает строки, `COUNT(x)` - известные значения; для пустого набора `SUM` равна NULL, `COUNT` равна 0.
* `GROUP BY` сворачивает строки в группы; `WHERE` фильтрует строки до группировки, `HAVING` - группы после. Негруппируемые столбцы в `SELECT` переносимо не работают.
* Оконные функции (SQLite ≥ 3.25.0) добавляют значение к каждой строке, не сворачивая её: `ROW_NUMBER`, `RANK`, `LAG`, `SUM() OVER`; `PARTITION BY` разбивает на группы, `ORDER BY` задаёт порядок.
* Рамка по умолчанию с `ORDER BY` включает равные строки; для построчных итогов задавайте `ROWS` и уникальный ключ.
* «Топ-N в группе»: `ROW_NUMBER` во вложенном запросе и фильтр во внешнем.
* Дальше: [проектирование схемы и нормализация](lesson:databases.sql.schema-design-and-normalization) - как устроить таблицы, чтобы такие запросы были простыми и данные не противоречили сами себе.
