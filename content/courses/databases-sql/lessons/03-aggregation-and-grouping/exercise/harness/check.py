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


import agg_task as m

DATA_A = [
    (1, "Anna", "Dev", 150000), (2, "Boris", "Dev", 120000), (3, "Vera", "Sales", 90000),
    (4, "Gleb", "Sales", None), (5, "Dina", None, 100000), (6, "Egor", "Ops", 70000),
    (7, "Zhanna", "Dev", 120000),
]
DATA_B = [
    (1, "Kim", "X", None), (2, "Lee", "X", None), (3, "Max", "Y", 50), (4, "Nia", "Y", 50),
    (5, "Oz", None, 40), (6, "Pia", "Z", 60),
]


def run(sql, rows):
    conn = sqlite3.connect(":memory:")
    conn.execute("CREATE TABLE employees(id INTEGER PRIMARY KEY, name TEXT NOT NULL, dept TEXT, salary INTEGER)")
    conn.executemany("INSERT INTO employees VALUES (?,?,?,?)", rows)
    return conn.execute(sql).fetchall()


def by_dept(rows):
    groups = {}
    for r in rows:
        if r[2] is not None:
            groups.setdefault(r[2], []).append(r)
    return dict(sorted(groups.items()))


def ranked(members):
    return sorted(members, key=lambda r: (r[3] is None, -(r[3] or 0), r[0]))


def expected(name, rows):
    if name == "Q_DEPT_STATS":
        out = []
        for d, ms in by_dept(rows).items():
            sal = [r[3] for r in ms if r[3] is not None]
            out.append((d, len(ms), sum(sal) if sal else None))
        return out
    if name == "Q_BIG_DEPTS":
        return [(d,) for d, ms in by_dept(rows).items() if len(ms) >= 2]
    if name == "Q_COUNTS":
        return [(len(rows), sum(1 for r in rows if r[3] is not None), len({r[2] for r in rows if r[2] is not None}))]
    if name == "Q_RANK":
        return [(r[0], d, i) for d, ms in by_dept(rows).items() for i, r in enumerate(ranked(ms), 1)]
    if name == "Q_RUNNING":
        total, out = 0, []
        for r in sorted(rows):
            total += r[3] or 0
            out.append((r[0], total))
        return out
    if name == "Q_TOP_PER_DEPT":
        return [(d, ranked(ms)[0][1]) for d, ms in by_dept(rows).items()]


def check_query(name, datasets):
    def check():
        sql = getattr(m, name)
        if not isinstance(sql, str) or not sql.strip().upper().startswith(("SELECT", "WITH")):
            return f"{name} должна быть строкой с запросом SELECT"
        for label, rows in datasets:
            got = run(sql, rows)
            want = expected(name, rows)
            if got != want:
                return f"{name} на наборе {label}: получено {got!r}, ожидалось {want!r}"
    return check


A = ("A", DATA_A)
B = ("B", DATA_B)
case("dept-stats", check_query("Q_DEPT_STATS", [A]))
case("big-depts", check_query("Q_BIG_DEPTS", [A]))
case("counts", check_query("Q_COUNTS", [A]))
case("dept-stats-null-sum", check_query("Q_DEPT_STATS", [B]))
case("rank-window", check_query("Q_RANK", [A, B]))
case("running-total", check_query("Q_RUNNING", [A, B]))
case("top-per-dept", check_query("Q_TOP_PER_DEPT", [A, B]))
