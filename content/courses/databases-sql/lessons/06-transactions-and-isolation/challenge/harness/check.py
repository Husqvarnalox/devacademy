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


import docs_task as m


def fresh():
    conn = sqlite3.connect(":memory:", autocommit=True)
    conn.execute("CREATE TABLE docs(id INTEGER PRIMARY KEY, body TEXT NOT NULL, version INTEGER NOT NULL)")
    conn.execute("INSERT INTO docs VALUES (1, 'a', 1)")
    return conn


def row(conn):
    return conn.execute("SELECT body, version FROM docs WHERE id = 1").fetchone()


def concurrent_append(conn, suffix):
    conn.execute("UPDATE docs SET body = body || ?, version = version + 1 WHERE id = 1", (suffix,))


def t_save_ok():
    conn = fresh()
    v = m.save(conn, 1, "b", 1)
    if v != 2 or row(conn) != ("b", 2):
        return f"save вернула {v!r}, документ {row(conn)!r}; ожидалось 2 и ('b', 2)"


def t_save_conflict():
    conn = fresh()
    m.save(conn, 1, "b", 1)
    try:
        m.save(conn, 1, "stale", 1)
    except m.Conflict:
        pass
    else:
        return "устаревшая версия должна давать Conflict"
    if row(conn) != ("b", 2):
        return f"конфликтное сохранение изменило документ: {row(conn)!r}"


def t_missing():
    conn = fresh()
    try:
        m.save(conn, 99, "x", 1)
    except LookupError:
        pass
    else:
        return "save для несуществующего документа должна поднимать LookupError"
    try:
        m.edit_with_retry(conn, 99, str.upper)
    except LookupError:
        pass
    else:
        return "edit_with_retry для несуществующего документа должна поднимать LookupError"


def t_retry_recovers():
    conn = fresh()
    calls = []
    hook_state = {"n": 0}

    def hook():
        hook_state["n"] += 1
        if hook_state["n"] == 1:
            concurrent_append(conn, "+other")

    def transform(body):
        calls.append(body)
        return body + "+mine"

    v = m.edit_with_retry(conn, 1, transform, max_attempts=3, _after_read=hook)
    if row(conn) != ("a+other+mine", 3) or v != 3:
        return f"документ {row(conn)!r}, версия из функции {v!r}; ожидалось ('a+other+mine', 3) и 3 (изменение конкурента не потеряно)"
    if calls != ["a", "a+other"]:
        return f"transform вызывали с {calls!r}; при повторе он должен получить свежий текст"


def t_exhausted():
    conn = fresh()
    calls = []

    def hook():
        concurrent_append(conn, "!")

    def transform(body):
        calls.append(body)
        return body + "#"

    try:
        m.edit_with_retry(conn, 1, transform, max_attempts=2, _after_read=hook)
    except m.Conflict:
        pass
    else:
        return "после исчерпания попыток ожидался Conflict"
    if len(calls) != 2:
        return f"transform вызвана {len(calls)} раз, ожидалось ровно max_attempts = 2"
    if row(conn) != ("a!!", 3):
        return f"документ {row(conn)!r}; наши неудавшиеся изменения не должны сохраняться"


case("save-ok", t_save_ok)
case("save-conflict", t_save_conflict)
case("missing-document", t_missing)
case("retry-recovers", t_retry_recovers)
case("retry-exhausted", t_exhausted)
