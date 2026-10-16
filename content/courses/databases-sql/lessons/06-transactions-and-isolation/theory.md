<!-- section: simple -->
## Всё или ничего

Вы переводите 30 рублей со счёта Анны на счёт Бориса. Для базы это два оператора: «уменьшить баланс Анны» и «увеличить баланс Бориса». Если сервер упадёт между ними, деньги исчезнут: у Анны списали, Борису не зачислили. Если два перевода пойдут одновременно, они могут перепутать друг другу итоговые суммы.

**Транзакция** - это группа операций, которую база обязуется выполнить как одно целое:

* `BEGIN` - начать группу;
* `COMMIT` - зафиксировать все изменения разом;
* `ROLLBACK` - отменить все изменения группы, как будто ничего не было.

Типичные гарантии транзакции описывает аббревиатура **ACID**: атомарность (всё или ничего), согласованность (ограничения `CHECK`, `FOREIGN KEY`, `UNIQUE` не нарушены), изоляция (параллельные транзакции не портят друг друга) и долговечность (после `COMMIT` данные пережили сбой).

В этом уроке вы узнаете, как управлять транзакциями из Python, что происходит, когда транзакции идут одновременно, и как написать перевод денег, который безопасно повторять. Урок опирается на [ограничения схемы](lesson:databases.sql.schema-design-and-normalization): именно `CHECK (balance >= 0)` превращает нехватку денег в ошибку, после которой нужен откат.

<!-- section: precise -->
## Транзакции в sqlite3, блокировки и изоляция

**Управление транзакциями в Python.** Модуль `sqlite3` в Python 3.12+ имеет три режима, выбираемых параметром `autocommit` функции `connect`:

| `autocommit` | поведение |
|---|---|
| `sqlite3.LEGACY_TRANSACTION_CONTROL` (по умолчанию) | старый режим: перед `INSERT`, `UPDATE`, `DELETE`, `REPLACE` модуль сам выполняет `BEGIN` (если `isolation_level` не `None`); перед `SELECT` и DDL - нет. Транзакцию заканчивают `conn.commit()` или `conn.rollback()` |
| `False` | транзакция всегда открыта: начинается при подключении и сразу после каждого `commit()`/`rollback()`. Закрытие соединения без `commit()` теряет изменения |
| `True` | модуль не открывает транзакции сам; `commit()` и `rollback()` **ничего не делают**. Транзакциями управляют операторами `BEGIN`, `COMMIT`, `ROLLBACK` |

Менеджер контекста `with conn:` не закрывает соединение. Он фиксирует транзакцию при нормальном выходе и откатывает при исключении (для `autocommit=True` он ничего не делает). В новом коде рекомендуется явно указывать `autocommit` вместо `isolation_level`. В этом курсе упражнение использует `autocommit=True` и явные `BEGIN IMMEDIATE`/`COMMIT`/`ROLLBACK`: так видно каждый шаг, а ошибка не прячется за неявным поведением.

**Блокировки SQLite.** SQLite допускает одного писателя на всю базу. Вид начала транзакции:

* `BEGIN` (`DEFERRED`) - блокировка берётся при первом обращении; если к этому моменту читатель уже видит старые данные, а писатель появился, попытка записи может завершиться ошибкой;
* `BEGIN IMMEDIATE` - блокировка записи берётся сразу; при занятости ждёт `timeout` (по умолчанию 5 секунд), затем `OperationalError: database is locked`;
* `BEGIN EXCLUSIVE` - как `IMMEDIATE`, а в режиме журнала отката ещё и закрывает базу для читателей.

Для операций «прочитать, решить, записать» используйте `BEGIN IMMEDIATE`: тогда решение принимается по данным, которые никто не изменит до `COMMIT`. В режиме журнала **WAL** (`PRAGMA journal_mode=WAL`) читатели и писатель не мешают друг другу: читатель видит снимок на момент начала своей транзакции, а писатель по-прежнему один.

**Аномалии при параллельной работе.**

| аномалия | что происходит |
|---|---|
| грязное чтение | транзакция читает ещё не зафиксированные данные другой |
| неповторяющееся чтение | повторное чтение той же строки в рамках транзакции даёт другое значение |
| фантом | повторный запрос по условию возвращает другой набор строк |
| потерянное обновление | две транзакции читают одно значение, обе пишут вычисленное в приложении, одна запись затирает другую |

**Уровни изоляции** (стандарт SQL) определяют, какие аномалии допустимы: `READ UNCOMMITTED`, `READ COMMITTED`, `REPEATABLE READ`, `SERIALIZABLE`. В **PostgreSQL** по умолчанию `READ COMMITTED`: каждый оператор видит данные, зафиксированные к его началу (грязных чтений нет, `READ UNCOMMITTED` ведёт себя так же). `REPEATABLE READ` в PostgreSQL показывает снимок на начало транзакции и фантомов не допускает; конфликтующая запись может завершиться ошибкой сериализации (код `40001`), и приложение должно **повторить транзакцию**. `SERIALIZABLE` ведёт себя так, будто транзакции выполнялись по одной. В **MySQL InnoDB** уровень по умолчанию - `REPEATABLE READ`. SQLite фактически работает как `SERIALIZABLE` для записи (один писатель), но по умолчанию читатель может видеть изменения, зафиксированные между его операторами, пока он не открыл транзакцию.

**Как предотвратить потерянное обновление.**

1. **Атомарный `UPDATE`**: `UPDATE accounts SET balance = balance + ? WHERE id = ?` - чтение и запись внутри одного оператора. Предпочтительный способ.
2. **Блокировка строки**: `SELECT ... FOR UPDATE` в PostgreSQL и MySQL блокирует выбранные строки до конца транзакции. В SQLite такого синтаксиса нет (блокируется вся база; используйте `BEGIN IMMEDIATE`).
3. **Оптимистичная блокировка**: в строке хранят `version`; запись `UPDATE ... SET version = version + 1 WHERE id = ? AND version = ?` не сработает (0 строк), если кто-то успел раньше. Приложение перечитывает данные и повторяет (см. испытание).

**Идемпотентность.** Сеть ненадёжна: клиент, не получивший ответа, повторит запрос. Операцию делают идемпотентной, записывая уникальный ключ запроса (`PRIMARY KEY`) **в той же транзакции**, что и сам перевод: повтор упрётся в нарушение уникальности и ничего не изменит.

<!-- section: examples -->
## Примеры

**Пример 1. Старый режим: `with conn` откатывает при ошибке.**

```python run
import sqlite3

conn = sqlite3.connect(":memory:")
conn.execute("CREATE TABLE accounts (id INTEGER PRIMARY KEY, balance INTEGER NOT NULL CHECK (balance >= 0))")
conn.executemany("INSERT INTO accounts VALUES (?, ?)", [(1, 100), (2, 50)])
conn.commit()

try:
    with conn:  # COMMIT при успехе, ROLLBACK при исключении
        conn.execute("UPDATE accounts SET balance = balance - 30 WHERE id = 1")
        print("внутри транзакции:", conn.in_transaction)
        conn.execute("UPDATE accounts SET balance = balance - 80 WHERE id = 2")  # нарушит CHECK
except sqlite3.IntegrityError as e:
    print("ошибка:", e)

print("после отката:", conn.execute("SELECT id, balance FROM accounts ORDER BY id").fetchall())
print("транзакция открыта:", conn.in_transaction)
```
```output
внутри транзакции: True
ошибка: CHECK constraint failed: balance >= 0
после отката: [(1, 100), (2, 50)]
транзакция открыта: False
```

Первое списание откатилось вместе со вторым, хотя само по себе было корректным: это и есть атомарность.

**Пример 2. `autocommit=True` и `autocommit=False`.**

```python run
import sqlite3

for mode in (True, False):
    conn = sqlite3.connect(":memory:", autocommit=mode)
    conn.execute("CREATE TABLE t (x INTEGER)")
    print(f"autocommit={mode}: после connect/CREATE in_transaction =", conn.in_transaction)
    if mode is False:
        conn.commit()  # зафиксировать CREATE, иначе rollback() отменит и его
    conn.execute("INSERT INTO t VALUES (1)")
    print("  после INSERT in_transaction =", conn.in_transaction)
    conn.rollback()  # при True ничего не делает
    print("  после rollback() строк:", conn.execute("SELECT count(*) FROM t").fetchone()[0])

conn = sqlite3.connect(":memory:", autocommit=True)
conn.execute("CREATE TABLE t (x INTEGER)")
conn.execute("BEGIN IMMEDIATE")
conn.execute("INSERT INTO t VALUES (1)")
conn.execute("ROLLBACK")  # явный оператор действует
print("явный BEGIN/ROLLBACK, строк:", conn.execute("SELECT count(*) FROM t").fetchone()[0])
```
```output
autocommit=True: после connect/CREATE in_transaction = False
  после INSERT in_transaction = False
  после rollback() строк: 1
autocommit=False: после connect/CREATE in_transaction = True
  после INSERT in_transaction = True
  после rollback() строк: 0
явный BEGIN/ROLLBACK, строк: 0
```

При `autocommit=True` вызов `rollback()` не отменил вставку: нужен именно оператор `ROLLBACK`. При `autocommit=False` транзакция есть всегда, поэтому `rollback()` её отменяет и сразу открывается следующая.

**Пример 3. Потерянное обновление и атомарный `UPDATE`.**

```python run
import os
import sqlite3
import tempfile

path = os.path.join(tempfile.mkdtemp(), "bank.db")
setup = sqlite3.connect(path, autocommit=True)
setup.execute("CREATE TABLE accounts (id INTEGER PRIMARY KEY, balance INTEGER NOT NULL)")
setup.execute("INSERT INTO accounts VALUES (1, 100)")
setup.close()

def deposit_unsafe(conn_a, conn_b, amount_a, amount_b):
    """Два клиента читают баланс, потом пишут значение, посчитанное в Python."""
    a = conn_a.execute("SELECT balance FROM accounts WHERE id = 1").fetchone()[0]
    b = conn_b.execute("SELECT balance FROM accounts WHERE id = 1").fetchone()[0]
    conn_a.execute("UPDATE accounts SET balance = ? WHERE id = 1", (a + amount_a,))
    conn_b.execute("UPDATE accounts SET balance = ? WHERE id = 1", (b + amount_b,))

c1 = sqlite3.connect(path, autocommit=True)
c2 = sqlite3.connect(path, autocommit=True)

deposit_unsafe(c1, c2, 10, 20)
print("небезопасно: ожидали 130, получили", c1.execute("SELECT balance FROM accounts").fetchone()[0])

c1.execute("UPDATE accounts SET balance = 100 WHERE id = 1")
c1.execute("UPDATE accounts SET balance = balance + ? WHERE id = 1", (10,))
c2.execute("UPDATE accounts SET balance = balance + ? WHERE id = 1", (20,))
print("атомарный UPDATE: ожидали 130, получили", c1.execute("SELECT balance FROM accounts").fetchone()[0])
```
```output
небезопасно: ожидали 130, получили 120
атомарный UPDATE: ожидали 130, получили 130
```

В небезопасном варианте значения 110 и 120 посчитаны из одного и того же старого 100, поэтому вторая запись затёрла первую. Атомарный `UPDATE` читает и пишет значение внутри одного оператора под блокировкой.

**Пример 4. Один писатель и снимок читателя.**

```python run
import os
import sqlite3
import tempfile

path = os.path.join(tempfile.mkdtemp(), "wal.db")
w = sqlite3.connect(path, autocommit=True, timeout=0)
print("режим журнала:", w.execute("PRAGMA journal_mode=WAL").fetchone()[0])
w.execute("CREATE TABLE accounts (id INTEGER PRIMARY KEY, balance INTEGER)")
w.execute("INSERT INTO accounts VALUES (1, 100)")

r = sqlite3.connect(path, autocommit=True, timeout=0)
r.execute("BEGIN")  # читающая транзакция
print("читатель видит:", r.execute("SELECT balance FROM accounts").fetchone()[0])

w.execute("BEGIN IMMEDIATE")
w.execute("UPDATE accounts SET balance = 999 WHERE id = 1")
print("читатель при открытой записи:", r.execute("SELECT balance FROM accounts").fetchone()[0])

other = sqlite3.connect(path, autocommit=True, timeout=0)
try:
    other.execute("BEGIN IMMEDIATE")
except sqlite3.OperationalError as e:
    print("второй писатель:", e)

w.execute("COMMIT")
print("читатель после COMMIT писателя:", r.execute("SELECT balance FROM accounts").fetchone()[0])
r.execute("COMMIT")
print("читатель в новой транзакции:", r.execute("SELECT balance FROM accounts").fetchone()[0])
```
```output
режим журнала: wal
читатель видит: 100
читатель при открытой записи: 100
второй писатель: database is locked
читатель после COMMIT писателя: 100
читатель в новой транзакции: 999
```

Читатель не блокировал писателя и сохранил свой снимок (100) даже после чужого `COMMIT`. Второй писатель при `timeout=0` сразу получил `database is locked`; с обычным `timeout=5.0` он бы подождал.

<!-- section: mistakes -->
## Типичные ошибки

* **Считать, что `commit()` работает всегда.** При `autocommit=True` `commit()` и `rollback()` ничего не делают; транзакцию, начатую `BEGIN`, нужно закрыть оператором. Забытая открытая транзакция держит блокировку и мешает другим.
* **Отсутствие отката при ошибке.** Исключение посреди перевода без `ROLLBACK` оставляет транзакцию открытой с частичными изменениями. Всегда `try`/`except` с откатом или `with conn:` в режиме с неявными транзакциями.
* **Читать значение, считать в Python, писать обратно** (`SELECT balance`, затем `UPDATE ... SET balance = <число>`). Это потерянное обновление. Считайте внутри `UPDATE` или блокируйте строку.
* **Игнорировать `rowcount`.** `UPDATE` несуществующего счёта не вызывает ошибку и меняет 0 строк; без проверки `rowcount` деньги списаны, а зачислены никому.
* **Идемпотентный ключ вне транзакции.** Если ключ записан отдельно от перевода, сбой между ними оставит ключ без перевода: повтор будет отвергнут, деньги не переведены.
* **Перенос привычек между СУБД.** `SELECT ... FOR UPDATE` в SQLite - синтаксическая ошибка. В MySQL DDL (`CREATE TABLE` и подобные) неявно завершает транзакцию, а в PostgreSQL и SQLite DDL откатывается вместе с ней.
* **Не повторять транзакцию при ошибке сериализации или `database is locked`.** В PostgreSQL на уровнях `REPEATABLE READ` и `SERIALIZABLE` конфликт - штатная ситуация (код `40001`); повторяйте всю транзакцию целиком, а не один оператор.
* **Долгие транзакции.** Транзакция, которая ждёт ответа внешнего сервиса, держит блокировки и мешает всем. Делайте её короткой: подготовьте данные заранее, внутри - только обращения к базе.

<!-- section: summary -->
## Итоги

* Транзакция делает группу операций атомарной: `COMMIT` фиксирует всё, `ROLLBACK` отменяет всё. ACID: атомарность, согласованность, изоляция, долговечность.
* В Python 3.12+ выбирайте режим явно: `autocommit=True` с операторами `BEGIN`/`COMMIT`/`ROLLBACK` (где `commit()` и `rollback()` не действуют) или `autocommit=False`; по умолчанию - прежнее поведение через `isolation_level`.
* SQLite: один писатель; `BEGIN IMMEDIATE` берёт блокировку сразу, WAL позволяет читать во время записи, занятая база даёт `database is locked`.
* Аномалии: грязное чтение, неповторяющееся чтение, фантом, потерянное обновление. PostgreSQL по умолчанию `READ COMMITTED`, MySQL InnoDB - `REPEATABLE READ`.
* Против потерянного обновления: атомарный `UPDATE`, `SELECT ... FOR UPDATE` (PostgreSQL/MySQL) или оптимистичная блокировка по версии.
* Идемпотентность достигается уникальным ключом запроса, записанным в одной транзакции с действием. Дальше: [безопасный доступ к базе и SQL-инъекции](lesson:databases.sql.sql-injection-and-safe-access).
