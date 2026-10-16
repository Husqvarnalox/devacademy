<!-- section: simple -->
## Зачем соединять

В хорошо спроектированной базе данные не повторяются: имя клиента хранится один раз в таблице `customers`, а в `orders` лежит только ссылка `customer_id`. Чтобы получить «заказ и имя клиента», таблицы нужно **соединить**: для каждой строки одной таблицы найти подходящие строки другой.

Представьте две стопки карточек. Соединение берёт карточку заказа, читает на ней номер клиента и достаёт из другой стопки карточку клиента с этим номером. Если подходящих карточек две, получится две пары. Если ни одной - вид соединения решает, что делать со «сиротой»: выбросить (`INNER JOIN`) или оставить с пустыми полями (`LEFT JOIN`).

Отсюда две главные идеи урока:

* **Вид соединения определяет, что станет со строками без пары.** Забыли про это - и запрос молча теряет клиентов без заказов.
* **Число строк результата - это число подходящих пар.** Если у заказа три позиции, после соединения заказ встретится три раза. Это называется размножением строк; на нём ломаются суммы и подсчёты.

В конце - частая задача «найти то, чего нет»: клиентов без заказов, товары без продаж. Её решают антисоединением, и здесь есть классическая ловушка с `NOT IN` и `NULL`. Опираемся на урок о [NULL и SELECT](lesson:databases.sql.relational-model-and-select).

<!-- section: precise -->
## Виды соединений

```sql
SELECT ... FROM a [INNER] JOIN b ON условие        -- только пары
SELECT ... FROM a LEFT [OUTER] JOIN b ON условие   -- все строки a, пары из b или NULL
SELECT ... FROM a RIGHT [OUTER] JOIN b ON условие  -- все строки b
SELECT ... FROM a FULL [OUTER] JOIN b ON условие   -- все строки обеих таблиц
SELECT ... FROM a CROSS JOIN b                     -- все комбинации (декартово произведение)
```

* **INNER JOIN** оставляет только строки, для которых `ON` истинно. Строки с `NULL` в ключе пары не имеют: `NULL = NULL` - это NULL.
* **LEFT JOIN** сохраняет каждую строку левой таблицы; если пары нет, столбцы правой таблицы равны `NULL`.
* **RIGHT** и **FULL** симметричны. В SQLite они поддерживаются **начиная с 3.39.0**; в этом курсе проверено на 3.53.4. Эквивалент RIGHT - LEFT с переставленными таблицами. В PostgreSQL поддерживаются все виды; **MySQL не поддерживает FULL OUTER JOIN** (его имитируют `UNION` двух запросов).
* **CROSS JOIN** даёт `|a| × |b|` строк; полезен для генерации комбинаций, а случайный - источник огромных результатов (если забыть условие `ON` или написать `FROM a, b` без `WHERE`).
* **Self-join** - соединение таблицы с самой собой через разные псевдонимы (например, сотрудник и его руководитель: `employees e LEFT JOIN employees m ON m.id = e.manager_id`).
* `USING (col)` - сокращение для равенства одноимённых столбцов; `NATURAL JOIN` соединяет по *всем* одноимённым столбцам и ломается при добавлении нового столбца - избегайте его.

**ON и WHERE для внешних соединений.** `ON` определяет, какие строки образуют пары; `WHERE` фильтрует уже готовый результат. Для `LEFT JOIN` условие на правую таблицу в `WHERE` (`o.status = 'paid'`) отбросит строки с `NULL` и превратит соединение во внутреннее. Чтобы сохранить клиентов без оплаченных заказов, поместите условие в `ON`: `LEFT JOIN orders o ON o.customer_id = c.id AND o.status = 'paid'`. Для INNER JOIN разница между `ON` и `WHERE` неважна.

**Кардинальность и размножение строк.** Связь *один ко многим* (клиент - заказы) даёт столько строк на клиента, сколько у него заказов. Цепочка клиент - заказы - позиции размножает строки на каждом шаге: число строк равно числу позиций (плюс клиенты и заказы без детей при LEFT JOIN). Связь *многие ко многим* соединяют через таблицу-связку, и тогда строки множатся по обе стороны. Если после соединения выбирают атрибуты «верхней» таблицы, строки повторяются; убирают повторы через `DISTINCT`, через агрегирование (следующий урок) или заменяют соединение на `EXISTS`/`IN`. Особенно опасно считать `SUM(orders.total)` после присоединения позиций: сумма заказа учтётся столько раз, сколько у него позиций.

**Антисоединение (строки без пары).** Три способа:

```sql
-- 1. NOT EXISTS: надёжный и обычно самый ясный
SELECT c.name FROM customers c
WHERE NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = c.id);
-- 2. LEFT JOIN + проверка ключа правой таблицы на NULL
SELECT c.name FROM customers c LEFT JOIN orders o ON o.customer_id = c.id WHERE o.id IS NULL;
-- 3. NOT IN: ОПАСЕН при NULL в подзапросе
SELECT c.name FROM customers c WHERE c.id NOT IN (SELECT customer_id FROM orders);
```

`x NOT IN (1, 2, NULL)` раскрывается в `x <> 1 AND x <> 2 AND x <> NULL`. Последнее сравнение - NULL, поэтому всё выражение никогда не бывает истинным, и запрос возвращает **ноль строк**, как только в подзапросе появился один `NULL`. Правило верно и для SQLite, и для PostgreSQL, и для MySQL. Проверяйте `IS NOT NULL` внутри подзапроса или используйте `NOT EXISTS`. Для проверки «есть пара» применяют `EXISTS` или `IN` (последний безопасен: при NULL просто не находит совпадения).

<!-- section: examples -->
## Примеры

**Пример 1. INNER и LEFT JOIN, размножение строк.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.executescript("""
CREATE TABLE customers(id INTEGER PRIMARY KEY, name TEXT);
CREATE TABLE orders(id INTEGER PRIMARY KEY, customer_id INTEGER, total INTEGER);
CREATE TABLE items(order_id INTEGER, product TEXT);
INSERT INTO customers VALUES (1,'Ann'),(2,'Bob'),(3,'Cy');
INSERT INTO orders VALUES (10,1,50),(11,1,20),(12,2,70);
INSERT INTO items VALUES (10,'pen'),(10,'ink'),(10,'cap'),(11,'pen'),(12,'book');
""")

q = "SELECT c.name, o.id FROM customers c {kind} JOIN orders o ON o.customer_id = c.id ORDER BY c.name, o.id"
print("INNER", conn.execute(q.format(kind="INNER")).fetchall())
print("LEFT ", conn.execute(q.format(kind="LEFT")).fetchall())

print("заказов:", conn.execute("SELECT COUNT(*) FROM orders").fetchone()[0])
print("после JOIN позиций:", conn.execute("SELECT COUNT(*) FROM orders o JOIN items i ON i.order_id = o.id").fetchone()[0])
print("SUM по заказам:", conn.execute("SELECT SUM(total) FROM orders").fetchone()[0])
print("SUM после JOIN позиций:", conn.execute("SELECT SUM(o.total) FROM orders o JOIN items i ON i.order_id = o.id").fetchone()[0])
```
```output
INNER [('Ann', 10), ('Ann', 11), ('Bob', 12)]
LEFT  [('Ann', 10), ('Ann', 11), ('Bob', 12), ('Cy', None)]
заказов: 3
после JOIN позиций: 5
SUM по заказам: 140
SUM после JOIN позиций: 240
```

Сумма после соединения выросла со 140 до 240: заказ 10 (50) встретился три раза. Это и есть размножение строк.

**Пример 2. ON против WHERE и версия SQLite для RIGHT/FULL.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.executescript("""
CREATE TABLE c(id INTEGER PRIMARY KEY, name TEXT);
CREATE TABLE o(id INTEGER PRIMARY KEY, cid INTEGER, status TEXT);
INSERT INTO c VALUES (1,'Ann'),(2,'Bob'),(3,'Cy');
INSERT INTO o VALUES (10,1,'paid'),(11,1,'new'),(12,2,'new');
""")
in_on = "SELECT c.name, o.id FROM c LEFT JOIN o ON o.cid = c.id AND o.status = 'paid' ORDER BY c.name, o.id"
in_where = "SELECT c.name, o.id FROM c LEFT JOIN o ON o.cid = c.id WHERE o.status = 'paid' ORDER BY c.name, o.id"
print("условие в ON   :", conn.execute(in_on).fetchall())
print("условие в WHERE:", conn.execute(in_where).fetchall())

print("RIGHT/FULL поддерживаются (SQLite >= 3.39.0):", sqlite3.sqlite_version_info >= (3, 39, 0))
conn.executescript("CREATE TABLE a(k); CREATE TABLE b(k); INSERT INTO a VALUES (1),(2); INSERT INTO b VALUES (2),(3);")
print(conn.execute("SELECT a.k, b.k FROM a FULL JOIN b ON a.k = b.k ORDER BY a.k, b.k").fetchall())
```
```output
условие в ON   : [('Ann', 10), ('Bob', None), ('Cy', None)]
условие в WHERE: [('Ann', 10)]
RIGHT/FULL поддерживаются (SQLite >= 3.39.0): True
[(None, 3), (1, None), (2, 2)]
```

Для `FULL JOIN` строки без пары есть с обеих сторон: `(1, None)` и `(None, 3)`.

**Пример 3. Антисоединение: NOT EXISTS против NOT IN при NULL.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.executescript("""
CREATE TABLE customers(id INTEGER PRIMARY KEY, name TEXT);
CREATE TABLE orders(id INTEGER PRIMARY KEY, customer_id INTEGER);
INSERT INTO customers VALUES (1,'Ann'),(2,'Bob'),(3,'Cy');
INSERT INTO orders VALUES (10,1),(11,2);
""")
not_in = "SELECT name FROM customers WHERE id NOT IN (SELECT customer_id FROM orders) ORDER BY name"
not_exists = "SELECT name FROM customers c WHERE NOT EXISTS (SELECT 1 FROM orders o WHERE o.customer_id = c.id) ORDER BY name"
left = "SELECT c.name FROM customers c LEFT JOIN orders o ON o.customer_id = c.id WHERE o.id IS NULL ORDER BY c.name"
for label, q in [("NOT IN", not_in), ("NOT EXISTS", not_exists), ("LEFT JOIN", left)]:
    print("до NULL ", label, conn.execute(q).fetchall())

conn.execute("INSERT INTO orders VALUES (12, NULL)")   # заказ без клиента
for label, q in [("NOT IN", not_in), ("NOT EXISTS", not_exists), ("LEFT JOIN", left)]:
    print("с NULL  ", label, conn.execute(q).fetchall())
```
```output
до NULL  NOT IN [('Cy',)]
до NULL  NOT EXISTS [('Cy',)]
до NULL  LEFT JOIN [('Cy',)]
с NULL   NOT IN []
с NULL   NOT EXISTS [('Cy',)]
с NULL   LEFT JOIN [('Cy',)]
```

<!-- section: mistakes -->
## Типичные ошибки

* **`NOT IN` с подзапросом, где возможен `NULL`.** Один `NULL` обнуляет весь результат. Используйте `NOT EXISTS`.
* **Фильтр по правой таблице в `WHERE` после `LEFT JOIN`.** Превращает его в `INNER JOIN`. Условие для сохранения «безпарных» строк кладут в `ON`.
* **Суммы после соединения «один ко многим».** Значения верхней таблицы повторяются; агрегируйте до соединения (подзапрос) или считайте по уникальным ключам.
* **`DISTINCT` как универсальное лекарство.** Он скрывает размножение, но не исправляет суммы и прячет логическую ошибку в соединении. Сначала поймите, почему строки множатся.
* **Забытое условие `ON`.** Получается `CROSS JOIN` и миллионы строк. В SQLite запрос без `ON` для `JOIN` просто разрешён и ведёт себя как перекрёстное произведение.
* **`NATURAL JOIN`.** Меняется схема - меняется смысл запроса. Всегда указывайте `ON` или `USING`.
* **Использование `RIGHT`/`FULL JOIN` в старой сборке SQLite.** До 3.39.0 это синтаксическая ошибка; проверьте `sqlite3.sqlite_version_info` и при необходимости переставьте таблицы и используйте `LEFT JOIN`. В MySQL `FULL JOIN` нет вообще.
* **Соединение по столбцу с NULL.** Строки с `NULL` в ключе пары не получают; если это важно, обработайте их отдельно (`LEFT JOIN`, `IS NULL`).
* **Неоднозначные имена столбцов.** Если `id` есть в обеих таблицах, пишите `c.id`, `o.id` и задавайте псевдонимы (`AS order_id`).

<!-- section: summary -->
## Итог

* JOIN сопоставляет строки по условию `ON`; `INNER` оставляет только пары, `LEFT` сохраняет все строки левой таблицы (с `NULL` справа).
* `RIGHT` и `FULL` в SQLite есть с версии 3.39.0 (проверено на 3.53.4); в MySQL `FULL OUTER JOIN` отсутствует.
* Число строк равно числу подходящих пар: «один ко многим» размножает верхние строки и искажает суммы.
* Условие по правой таблице при `LEFT JOIN` кладите в `ON`, а не в `WHERE`.
* Строки без пары ищите через `NOT EXISTS` или `LEFT JOIN ... IS NULL`; `NOT IN` ломается, если в подзапросе есть `NULL`.
* Дальше: [агрегирование и группировка](lesson:databases.sql.aggregation-and-grouping) - как сворачивать размноженные строки правильно.
