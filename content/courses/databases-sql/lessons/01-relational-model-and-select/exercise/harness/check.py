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


import queries as m

DATA_A = [
    (1, "Anna", "Dev", 150000, None),
    (2, "Boris", "Dev", 120000, 1),
    (3, "Vera", "Sales", 90000, 1),
    (4, "Gleb", "Sales", None, 3),
    (5, "Dina", None, 100000, 1),
    (6, "Egor", "Ops", 70000, 2),
    (7, "Zhanna", "Dev", 120000, 2),
]
DATA_B = [
    (1, "Kim", None, None, None),
    (2, "Lee", "Sales", 50000, None),
    (3, "Max", "Sales", None, 2),
    (4, "Nia", "Ops", 50000, 2),
    (5, "Oz", "Ops", 40000, None),
    (6, "Pia", None, 60000, 4),
]


def run(sql, rows):
    conn = sqlite3.connect(":memory:")
    conn.execute("CREATE TABLE employees(id INTEGER PRIMARY KEY, name TEXT NOT NULL, dept TEXT, salary INTEGER, manager_id INTEGER)")
    conn.executemany("INSERT INTO employees VALUES (?,?,?,?,?)", rows)
    cur = conn.execute(sql)
    return cur.fetchall(), [d[0] for d in cur.description]


def expected(name, rows):
    if name == "lowest":
        return sorted([(r[1], r[3]) for r in rows if r[3] is not None], key=lambda t: (t[1], t[0]))[:2]
    if name == "no_manager":
        return [(r[1],) for r in sorted(rows, key=lambda r: r[1]) if r[4] is None]
    if name == "page2":
        return [(r[0], r[1]) for r in sorted(rows)][2:4]
    if name == "not_sales":
        return [(r[0],) for r in sorted(rows) if r[2] != "Sales"]
    if name == "annual":
        return sorted([(r[1], (r[3] or 0) * 12) for r in rows])
    if name == "depts":
        return [(d,) for d in sorted({r[2] for r in rows if r[2] is not None})]


QUERIES = {
    "lowest": "Q_LOWEST_PAID", "no_manager": "Q_NO_MANAGER", "page2": "Q_PAGE2",
    "not_sales": "Q_NOT_SALES", "annual": "Q_ANNUAL", "depts": "Q_DEPTS",
}


def check_query(name, datasets):
    def check():
        const = QUERIES[name]
        sql = getattr(m, const)
        if not isinstance(sql, str) or not sql.strip().upper().startswith("SELECT"):
            return f"{const} должна быть строкой с запросом SELECT"
        for label, rows in datasets:
            got, cols = run(sql, rows)
            want = expected(name, rows)
            if got != want:
                return f"{const} на наборе {label}: получено {got!r}, ожидалось {want!r}"
            if name == "annual" and cols != ["name", "annual"]:
                return f"имена столбцов {cols!r}, ожидалось ['name', 'annual']"
    return check


A = ("A", DATA_A)
B = ("B", DATA_B)
case("lowest-paid", check_query("lowest", [A]))
case("no-manager", check_query("no_manager", [A]))
case("page2", check_query("page2", [A]))
case("not-sales-null", check_query("not_sales", [A, B]))
case("annual-coalesce", check_query("annual", [A, B]))
case("distinct-depts", check_query("depts", [A, B]))
case("other-data", lambda: check_query("lowest", [B])() or check_query("no_manager", [B])() or check_query("page2", [B])())
