<!-- section: simple -->
## Каждый факт - в одном месте

Представьте, что вы ведёте учёт заказов в одной большой таблице: номер заказа, товар, имя клиента, город клиента, адрес склада. Клиент сделал двадцать заказов - и его имя и город записаны двадцать раз. Он переехал: вы исправили город в пятнадцати строках и пропустили пять. Теперь в базе два разных «правдивых» города одного клиента, и никто не знает, какой верный.

Это и есть главная проблема плохой схемы: **один факт хранится в нескольких местах**. **Нормализация** - набор правил, как разложить данные по таблицам так, чтобы каждый факт жил ровно в одном месте, а таблицы связывались ключами. Тогда исправление города - это изменение одной строки.

Три вида неприятностей (аномалий) при избыточной схеме:

* **Аномалия обновления** - изменили факт в одной копии, в других он остался прежним.
* **Аномалия вставки** - нельзя записать факт без другого. Нового клиента нельзя добавить, пока у него нет заказа, потому что клиент хранится только внутри заказов.
* **Аномалия удаления** - удалили заказ и вместе с ним потеряли единственные сведения о клиенте.

Вторая половина урока - **ограничения**: правила, которые база проверяет сама (`NOT NULL`, `UNIQUE`, `CHECK`, `FOREIGN KEY`). Ошибка в приложении не должна приводить к «заказу несуществующего клиента»: такие вещи надёжнее запретить на уровне схемы. Урок продолжает [агрегирование](lesson:databases.sql.aggregation-and-grouping) и [соединения](lesson:databases.sql.joins): хорошая схема делает запросы простыми.

<!-- section: precise -->
## Нормальные формы и ограничения

**Функциональная зависимость** `A -> B` означает: значение `A` однозначно определяет значение `B` (по `customer_id` определяется `customer_city`). **Ключ** - минимальный набор столбцов, определяющий всю строку.

* **1NF.** Значения в ячейках *атомарны*: нет списков («1,2,3» в одном столбце), нет повторяющихся групп столбцов (`phone1`, `phone2`, `phone3`), у таблицы есть ключ. Практически это значит: вместо списка заводят отдельную таблицу со строкой на элемент.
* **2NF.** Таблица в 1NF, и каждый неключевой столбец зависит от **всего** ключа, а не от его части. Нарушение возможно только при составном ключе: в `enrollments(student_id, course_id, student_name, grade)` имя определяется одним `student_id`. Лекарство: вынести `student_name` в `students`.
* **3NF.** Таблица в 2NF, и неключевые столбцы не зависят друг от друга (нет *транзитивных* зависимостей `ключ -> A -> B`). В `employees(id, dept_id, dept_name)` имя отдела определяется `dept_id`, а не `id`: его выносят в `departments`. Классическая формулировка: «каждый неключевой атрибут зависит от ключа, от всего ключа и ни от чего, кроме ключа».
* Более строгие формы (BCNF, 4NF) нужны реже; на практике схему доводят до 3NF и отступают от неё осознанно.

**Ключи.** *Естественный* ключ - из предметной области (ISBN, email), *суррогатный* - искусственный (`id`). Суррогатный ключ не меняется при изменении бизнес-данных, естественный стоит держать как `UNIQUE`. В SQLite `INTEGER PRIMARY KEY` - псевдоним внутреннего `rowid` и присваивается автоматически. Составной ключ (`PRIMARY KEY (a, b)`) подходит для таблиц-связок. **Внешний ключ** `REFERENCES t(id)` требует, чтобы значение существовало в родительской таблице (или было NULL, если столбец не объявлен `NOT NULL`).

**Связи.** *Один ко многим*: внешний ключ в таблице «многих» (`orders.customer_id`). *Многие ко многим*: отдельная таблица-связка с двумя внешними ключами и составным первичным ключом (`enrollments`). *Один к одному*: внешний ключ с `UNIQUE`, используется редко.

**Ограничения.**

* `NOT NULL` - значение обязательно. `UNIQUE` - значения не повторяются (NULL в SQLite и PostgreSQL допускается несколько раз). `CHECK (условие)` - условие должно быть true или NULL (NULL проходит!). `DEFAULT` - значение при вставке.
* `ON DELETE` / `ON UPDATE` для внешнего ключа: `NO ACTION` (по умолчанию; ошибка при нарушении, проверка в конце оператора), `RESTRICT` (ошибка сразу), `CASCADE` (изменить или удалить зависимые строки), `SET NULL`, `SET DEFAULT`. `CASCADE` уместен, когда дочерние строки не существуют сами по себе (записи студента), `RESTRICT` - когда удаление родителя с зависимыми данными это ошибка (курс с записями).

**SQLite и ограничения.** Проверка внешних ключей **по умолчанию выключена**: нужно выполнить `PRAGMA foreign_keys = ON` в *каждом* соединении (до начала транзакции). PostgreSQL и MySQL (InnoDB) проверяют их всегда. Типы в SQLite - *сродство*: значение другого типа сохраняется как есть (в `INTEGER` можно записать `'abc'`). `CREATE TABLE ... STRICT` (SQLite ≥ 3.37.0, проверено на 3.53.4) включает проверку типов (`INT`, `INTEGER`, `REAL`, `TEXT`, `BLOB`, `ANY`). В PostgreSQL и MySQL типы строгие, плюс есть богатые типы (`DATE`, `NUMERIC`, `BOOLEAN`, `JSONB`). Деньги храните целыми числами в минимальных единицах или `NUMERIC` (в SQLite - целыми копейками), но не `REAL`. Составной первичный ключ без `INTEGER PRIMARY KEY` в обычной таблице SQLite допускает NULL, если не объявлен `NOT NULL`.

**Когда денормализовать.** Отступать от 3NF разумно, когда измерение показало, что чтение дорого: предвычисленные счётчики и суммы, копия названия в таблице отчёта, плоская витрина для аналитики. Цена: нужно поддерживать копии согласованными (транзакция, триггер, фоновый пересчёт). Не путайте с *историей*: цена товара в строке заказа (`order_items.unit_price`) - не дубликат `products.price`, а факт «по какой цене продали».

**Синтаксис, различающийся между СУБД.** Автоинкремент: `INTEGER PRIMARY KEY` (SQLite), `GENERATED ... AS IDENTITY` (PostgreSQL), `AUTO_INCREMENT` (MySQL). Изменение схемы: `ALTER TABLE` в SQLite ограничен (нельзя добавить ограничение к существующему столбцу; обычно создают новую таблицу и копируют данные). UPSERT: `INSERT ... ON CONFLICT (k) DO UPDATE SET ... = excluded....` одинаков в SQLite (≥ 3.24.0) и PostgreSQL, MySQL использует `INSERT ... ON DUPLICATE KEY UPDATE`.

<!-- section: examples -->
## Примеры

**Пример 1. Аномалии плоской таблицы и нормализованная схема.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
# Плоская таблица: клиент повторяется в каждом заказе
conn.execute("CREATE TABLE flat(order_id INTEGER, product TEXT, customer TEXT, city TEXT)")
conn.executemany("INSERT INTO flat VALUES (?, ?, ?, ?)", [
    (1, "pen", "Ann", "Moscow"), (2, "ink", "Ann", "Moscow"), (3, "book", "Bob", "Kazan"),
])
# аномалия обновления: поменяли город только в одной строке
conn.execute("UPDATE flat SET city = 'Samara' WHERE order_id = 1")
print("города Ann:", conn.execute("SELECT DISTINCT city FROM flat WHERE customer = 'Ann' ORDER BY city").fetchall())
# аномалия удаления: удалили единственный заказ Bob - и потеряли самого Bob
conn.execute("DELETE FROM flat WHERE customer = 'Bob'")
print("Bob остался в базе:", bool(conn.execute("SELECT 1 FROM flat WHERE customer = 'Bob'").fetchall()))

# Нормализованный вариант (3NF)
conn.executescript("""
PRAGMA foreign_keys = ON;
CREATE TABLE customers(id INTEGER PRIMARY KEY, name TEXT NOT NULL, city TEXT NOT NULL);
CREATE TABLE products(id INTEGER PRIMARY KEY, title TEXT NOT NULL UNIQUE);
CREATE TABLE orders(id INTEGER PRIMARY KEY, customer_id INTEGER NOT NULL REFERENCES customers(id));
CREATE TABLE order_items(
    order_id   INTEGER NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
    product_id INTEGER NOT NULL REFERENCES products(id),
    qty        INTEGER NOT NULL CHECK (qty > 0),
    PRIMARY KEY (order_id, product_id)          -- таблица-связка "многие ко многим"
);
INSERT INTO customers VALUES (1, 'Ann', 'Moscow'), (2, 'Bob', 'Kazan');
INSERT INTO products VALUES (1, 'pen'), (2, 'ink');
INSERT INTO orders VALUES (1, 1), (2, 1);
INSERT INTO order_items VALUES (1, 1, 2), (1, 2, 1), (2, 1, 5);
UPDATE customers SET city = 'Samara' WHERE id = 1;     -- один факт - одно обновление
""")
sql = """SELECT c.name, c.city, p.title, i.qty FROM order_items i
         JOIN orders o ON o.id = i.order_id JOIN customers c ON c.id = o.customer_id
         JOIN products p ON p.id = i.product_id ORDER BY o.id, p.title"""
for row in conn.execute(sql):
    print(row)
```
```output
города Ann: [('Moscow',), ('Samara',)]
Bob остался в базе: False
('Ann', 'Samara', 'ink', 1)
('Ann', 'Samara', 'pen', 2)
('Ann', 'Samara', 'pen', 5)
```

Клиент без заказов (Bob) теперь существует независимо - аномалии вставки и удаления исчезли, а смена города меняет одну строку.

**Пример 2. Ограничения и внешние ключи: что происходит без `PRAGMA foreign_keys`.**

```python run
import sqlite3

DDL = """
CREATE TABLE users(id INTEGER PRIMARY KEY, email TEXT NOT NULL UNIQUE);
CREATE TABLE posts(
    id INTEGER PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    rating INTEGER CHECK (rating BETWEEN 1 AND 5)
);
"""

def attempt(conn, sql, params=()):
    try:
        conn.execute(sql, params)
        return "OK"
    except sqlite3.Error as exc:
        return f"{type(exc).__name__}: {exc}"

off = sqlite3.connect(":memory:")
off.executescript(DDL)
print("foreign_keys =", off.execute("PRAGMA foreign_keys").fetchone()[0])
print("без PRAGMA  :", attempt(off, "INSERT INTO posts(user_id) VALUES (42)"))

on = sqlite3.connect(":memory:")
on.execute("PRAGMA foreign_keys = ON")
on.executescript(DDL)
print("foreign_keys =", on.execute("PRAGMA foreign_keys").fetchone()[0])
print("с PRAGMA    :", attempt(on, "INSERT INTO posts(user_id) VALUES (42)"))
print(attempt(on, "INSERT INTO users(email) VALUES ('a@x.org')"))
print(attempt(on, "INSERT INTO users(email) VALUES ('a@x.org')"))
print(attempt(on, "INSERT INTO users(email) VALUES (NULL)"))
print(attempt(on, "INSERT INTO posts(user_id, rating) VALUES (1, 9)"))
print(attempt(on, "INSERT INTO posts(user_id, rating) VALUES (1, NULL)"))   # NULL проходит CHECK
on.execute("DELETE FROM users WHERE id = 1")                              # CASCADE убирает посты
print("постов после удаления пользователя:", on.execute("SELECT COUNT(*) FROM posts").fetchone()[0])
```
```output
foreign_keys = 0
без PRAGMA  : OK
foreign_keys = 1
с PRAGMA    : IntegrityError: FOREIGN KEY constraint failed
OK
IntegrityError: UNIQUE constraint failed: users.email
IntegrityError: NOT NULL constraint failed: users.email
IntegrityError: CHECK constraint failed: rating BETWEEN 1 AND 5
OK
постов после удаления пользователя: 0
```

**Пример 3. Сродство типов и STRICT-таблицы.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.execute("CREATE TABLE loose(n INTEGER)")
conn.execute("CREATE TABLE strict_t(n INTEGER) STRICT")
for value in ("42", "abc", 3.5):
    conn.execute("INSERT INTO loose VALUES (?)", (value,))
print(conn.execute("SELECT n, typeof(n) FROM loose ORDER BY rowid").fetchall())

for value in ("42", "abc", 3.5):
    try:
        conn.execute("INSERT INTO strict_t VALUES (?)", (value,))
    except sqlite3.IntegrityError as exc:
        print("STRICT отверг", repr(value), "->", exc)
print(conn.execute("SELECT n, typeof(n) FROM strict_t ORDER BY rowid").fetchall())

# Первичный ключ не INTEGER в обычной таблице допускает NULL, если не объявлен NOT NULL
conn.execute("CREATE TABLE legacy(k TEXT PRIMARY KEY)")
conn.execute("INSERT INTO legacy VALUES (NULL)")
print("NULL в PRIMARY KEY:", conn.execute("SELECT COUNT(*) FROM legacy WHERE k IS NULL").fetchone()[0])
```
```output
[(42, 'integer'), ('abc', 'text'), (3.5, 'real')]
STRICT отверг 'abc' -> cannot store TEXT value in INTEGER column strict_t.n
STRICT отверг 3.5 -> cannot store REAL value in INTEGER column strict_t.n
[(42, 'integer')]
NULL в PRIMARY KEY: 1
```

В обычной таблице `'abc'` и `3.5` сохранились без изменений, а `'42'` было преобразовано в число, потому что у столбца сродство `INTEGER`. `STRICT` отвергает то, что нельзя привести к типу столбца.

<!-- section: mistakes -->
## Типичные ошибки

* **Забыть `PRAGMA foreign_keys = ON`.** В SQLite внешние ключи молча не проверяются; включайте в каждом соединении сразу после `connect`.
* **Списки в ячейке** (`tags = 'a,b,c'`). Нельзя искать, считать и ссылаться на элементы; заведите таблицу и связку.
* **Повторяющиеся столбцы** (`phone1`, `phone2`). Ограничивает число значений и усложняет запросы.
* **Дублирование атрибутов клиента в заказах.** Аномалии обновления; вынесите клиента.
* **Путать историю и избыточность.** Цену на момент покупки хранят в строке заказа намеренно; иначе изменение прайса «перепишет» старые заказы.
* **Деньги в `REAL`.** Погрешности двоичной дроби; храните целое число копеек или `NUMERIC`/`DECIMAL` в СУБД, которая его поддерживает.
* **Ограничения только в приложении.** Другое приложение, скрипт или гонка запросов обойдут проверку. Дублируйте правила `NOT NULL`, `UNIQUE`, `CHECK`, `FOREIGN KEY` в схеме.
* **Ожидать, что `CHECK` отвергнет NULL.** `CHECK (rating BETWEEN 1 AND 5)` пропускает NULL; добавьте `NOT NULL`, если значение обязательно.
* **`ON DELETE CASCADE` по умолчанию «на всякий случай».** Удаление одной строки может каскадно удалить много данных. Используйте `RESTRICT`, когда зависимые данные ценны.
* **Преждевременная денормализация.** Сначала нормализуйте и измерьте; копии данных требуют поддержки согласованности.
* **Полагаться на `AUTOINCREMENT` для идентификации.** В SQLite `INTEGER PRIMARY KEY` уже даёт автоматические значения; ключевое слово `AUTOINCREMENT` нужно только для запрета повторного использования старых номеров и замедляет вставки.

<!-- section: summary -->
## Итог

* Нормализация хранит каждый факт один раз и устраняет аномалии обновления, вставки и удаления.
* 1NF - атомарные значения и ключ; 2NF - неключевые столбцы зависят от всего ключа; 3NF - и не друг от друга. Связь «многие ко многим» - это таблица-связка.
* Правила данных выражают ограничениями `PRIMARY KEY`, `FOREIGN KEY` (с `ON DELETE`), `UNIQUE`, `NOT NULL`, `CHECK`; `CHECK` пропускает NULL.
* В SQLite внешние ключи нужно включать `PRAGMA foreign_keys = ON` в каждом соединении; типы - сродство, строгие типы даёт `STRICT` (≥ 3.37.0). PostgreSQL и MySQL проверяют и то и другое всегда.
* Денормализация оправдана измерениями и требует поддержки согласованности; историческая копия значения - не избыточность.
* Дальше: [индексы и планы запросов](lesson:databases.sql.indexes-and-query-plans) - как сделать запросы по такой схеме быстрыми.
