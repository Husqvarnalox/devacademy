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


import datetime

import streaks_task as m


def run(rows):
    conn = sqlite3.connect(":memory:")
    conn.execute("CREATE TABLE logins(user TEXT, day TEXT)")
    conn.executemany("INSERT INTO logins VALUES (?,?)", rows)
    return conn.execute(m.Q_STREAKS).fetchall()


def expected(rows):
    out = []
    for user in sorted({r[0] for r in rows}):
        days = sorted({datetime.date.fromisoformat(r[1]) for r in rows if r[0] == user})
        start = prev = None
        for d in days + [None]:
            if prev is not None and d is not None and (d - prev).days == 1:
                prev = d
                continue
            if start is not None and (prev - start).days >= 1:
                out.append((user, start.isoformat(), prev.isoformat(), (prev - start).days + 1))
            start = prev = d
    return sorted(out, key=lambda r: (r[0], r[1]))


def check_rows(rows):
    def check():
        if not isinstance(m.Q_STREAKS, str):
            return "Q_STREAKS должна быть строкой"
        got = run(rows)
        want = expected(rows)
        if got != want:
            return f"получено {got!r}, ожидалось {want!r}"
    return check


BASIC = [("ann", "2024-05-01"), ("ann", "2024-05-02"), ("ann", "2024-05-03"), ("ann", "2024-05-07"),
         ("bob", "2024-05-10"), ("bob", "2024-05-12"), ("bob", "2024-05-13")]
SINGLE = [("cy", "2024-01-01"), ("cy", "2024-01-03"), ("cy", "2024-01-05"), ("di", "2024-01-01"), ("di", "2024-01-02")]
CALENDAR = [("ev", "2024-02-28"), ("ev", "2024-02-29"), ("ev", "2024-03-01"), ("ev", "2023-12-31"), ("ev", "2024-01-01")]
DUPS = [("fy", "2024-06-01"), ("fy", "2024-06-01"), ("fy", "2024-06-02"), ("fy", "2024-06-02"), ("fy", "2024-06-02")]
TWO = [("gu", "2024-07-01"), ("gu", "2024-07-02"), ("gu", "2024-07-04"), ("gu", "2024-07-05"), ("gu", "2024-07-06"),
       ("hi", "2024-07-02"), ("hi", "2024-07-03")]

case("streaks-basic", check_rows(BASIC))
case("streaks-singles-excluded", check_rows(SINGLE))
case("streaks-calendar-boundaries", check_rows(CALENDAR))
case("streaks-duplicates", check_rows(DUPS))
case("streaks-several-per-user", check_rows(TWO))
