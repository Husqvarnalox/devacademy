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


import re

import indexes_task as m

QUERIES = {
    "Q1": ("SELECT * FROM orders WHERE customer_id = ?", (5,)),
    "Q2": ("SELECT * FROM orders WHERE customer_id = ? AND status = ?", (5, "paid")),
    "Q3": ("SELECT * FROM orders WHERE status = ? AND created_at >= ?", ("paid", "2024-06-01")),
    "Q4": ("SELECT customer_id, total FROM orders WHERE customer_id = ?", (5,)),
    "Q5": ("SELECT * FROM orders WHERE status = ?", ("paid",)),
}
STATUSES = ["new", "paid", "shipped", "done", "cancelled"]
CREATE = re.compile(r"^\s*CREATE\s+(UNIQUE\s+)?INDEX\s", re.I)


def build():
    items = m.INDEXES
    if not isinstance(items, (list, tuple)) or not all(isinstance(s, str) for s in items):
        raise TypeError("INDEXES должен быть списком строк")
    for s in items:
        if not CREATE.match(s) or ";" in s.strip().rstrip(";"):
            raise ValueError(f"допустимы только отдельные операторы CREATE INDEX, получено: {s!r}")
    conn = sqlite3.connect(":memory:")
    conn.execute(
        "CREATE TABLE orders(id INTEGER PRIMARY KEY, customer_id INTEGER NOT NULL, status TEXT NOT NULL, "
        "created_at TEXT NOT NULL, total INTEGER NOT NULL, note TEXT)"
    )
    conn.executemany(
        "INSERT INTO orders(customer_id, status, created_at, total, note) VALUES (?,?,?,?,?)",
        [(i % 200, STATUSES[i % 5], "2024-%02d-%02d" % (i % 12 + 1, i % 28 + 1), i % 977, "n%d" % i) for i in range(3000)],
    )
    before = {r[0] for r in conn.execute("SELECT name FROM sqlite_master WHERE type = 'index'")}
    for s in items:
        conn.execute(s)
    names = {r[0] for r in conn.execute("SELECT name FROM sqlite_master WHERE type = 'index'")} - before
    return conn, names


def plan(conn, key):
    sql, params = QUERIES[key]
    return " | ".join(r[3] for r in conn.execute("EXPLAIN QUERY PLAN " + sql, params))


def needs(key, *fragments, covering=False):
    def check():
        conn, _ = build()
        text = plan(conn, key)
        if "SEARCH" not in text or "INDEX" not in text:
            return f"{key}: план «{text}» - нужен SEARCH ... USING INDEX"
        for frag in fragments:
            if frag not in text:
                return f"{key}: в плане «{text}» нет «{frag}»; проверьте порядок столбцов индекса"
        if covering and "COVERING INDEX" not in text:
            return f"{key}: план «{text}» - нужен COVERING INDEX (в индексе должны быть все читаемые столбцы)"
    return check


def t_no_useless():
    conn, names = build()
    used = set()
    for key in QUERIES:
        used |= set(re.findall(r"INDEX (\w+)", plan(conn, key)))
    unused = sorted(names - used)
    if unused:
        return f"индексы {unused} не используются ни одним из пяти запросов: они только замедляют запись"
    if not names:
        return "не создано ни одного индекса"


def t_limit():
    conn, names = build()
    if len(names) > 3:
        return f"создано {len(names)} индексов, допустимо не больше 3"
    if len(m.INDEXES) != len(names):
        return "каждый оператор должен создавать один новый индекс"


case("q1-customer", needs("Q1", "customer_id=?"))
case("q2-customer-status", needs("Q2", "customer_id=?", "status=?"))
case("q3-status-range", needs("Q3", "status=?", "created_at>"))
case("q4-covering", needs("Q4", "customer_id=?", covering=True))
case("q5-leftmost-status", needs("Q5", "status=?"))
case("no-useless-index", t_no_useless)
case("index-count-limit", t_limit)
