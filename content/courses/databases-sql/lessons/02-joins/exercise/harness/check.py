import json
import os
import sqlite3

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


import joins_task as m

CUSTOMERS = [(1, "Ann"), (2, "Bob"), (3, "Cy"), (4, "Di")]
ORDERS = [(10, 1, 50), (11, 1, 20), (12, 2, 70), (13, 3, 5)]
ITEMS = [(10, "pen", 2), (10, "ink", 3), (11, "pen", 1), (12, "book", 3), (13, "ink", 5)]
# Набор N: заказ с customer_id NULL и заказ с несуществующим клиентом.
ORDERS_N = ORDERS + [(14, None, 9), (15, 99, 1)]
ITEMS_N = ITEMS + [(14, "pen", 4), (15, "ink", 2)]
CLEAN = ("clean", CUSTOMERS, ORDERS, ITEMS)
NULLS = ("with-null", CUSTOMERS, ORDERS_N, ITEMS_N)


def run(sql, data):
    _, customers, orders, items = data
    conn = sqlite3.connect(":memory:")
    conn.executescript(
        "CREATE TABLE customers(id INTEGER PRIMARY KEY, name TEXT NOT NULL);"
        "CREATE TABLE orders(id INTEGER PRIMARY KEY, customer_id INTEGER, total INTEGER);"
        "CREATE TABLE order_items(order_id INTEGER, product TEXT, qty INTEGER);"
    )
    conn.executemany("INSERT INTO customers VALUES (?,?)", customers)
    conn.executemany("INSERT INTO orders VALUES (?,?,?)", orders)
    conn.executemany("INSERT INTO order_items VALUES (?,?,?)", items)
    return conn.execute(sql).fetchall()


def expected(name, data):
    _, customers, orders, items = data
    cname = dict(customers)
    ocust = {o[0]: o[1] for o in orders}
    if name == "Q_ORDERS_WITH_NAMES":
        return sorted((o[0], cname[o[1]], o[2]) for o in orders if o[1] in cname)
    if name == "Q_ALL_CUSTOMERS":
        rows = []
        for cid, n in customers:
            mine = sorted(o[0] for o in orders if o[1] == cid)
            rows += [(n, oid) for oid in mine] or [(n, None)]
        return sorted(rows, key=lambda r: (r[0], -1 if r[1] is None else r[1]))
    if name == "Q_NO_ORDERS":
        have = {o[1] for o in orders}
        return sorted((n,) for cid, n in customers if cid not in have)
    if name == "Q_ORPHAN_ORDERS":
        return sorted((o[0],) for o in orders if o[1] is not None and o[1] not in cname)
    if name == "Q_ITEMS":
        return sorted((cname[ocust[i[0]]], i[1], i[2]) for i in items if ocust.get(i[0]) in cname)
    if name == "Q_BIG_BUYERS":
        return sorted({(cname[ocust[i[0]]],) for i in items if i[2] >= 2 and ocust.get(i[0]) in cname})


def check_query(name, datasets):
    def check():
        sql = getattr(m, name)
        if not isinstance(sql, str) or not sql.strip().upper().startswith(("SELECT", "WITH")):
            return f"{name} должна быть строкой с запросом SELECT"
        for data in datasets:
            got = run(sql, data)
            want = expected(name, data)
            if got != want:
                return f"{name} на наборе {data[0]}: получено {got!r}, ожидалось {want!r}"
    return check


case("orders-with-names", check_query("Q_ORDERS_WITH_NAMES", [CLEAN]))
case("all-customers", check_query("Q_ALL_CUSTOMERS", [CLEAN]))
case("no-orders", check_query("Q_NO_ORDERS", [CLEAN]))
case("no-orders-null", check_query("Q_NO_ORDERS", [NULLS]))
case("orphans", check_query("Q_ORPHAN_ORDERS", [CLEAN, NULLS]))
case("items-three-joins", check_query("Q_ITEMS", [CLEAN, NULLS]))
case("big-buyers-distinct", check_query("Q_BIG_BUYERS", [CLEAN, NULLS]))
