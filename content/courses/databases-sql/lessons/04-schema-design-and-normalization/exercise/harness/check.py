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


import schema_task as m


def fresh():
    conn = m.connect()
    if not isinstance(conn, sqlite3.Connection):
        raise TypeError("connect() должна вернуть sqlite3.Connection")
    return conn


def add_basic(conn):
    conn.execute("INSERT INTO students(email, name) VALUES ('a@x.org', 'Ann')")
    conn.execute("INSERT INTO students(email, name) VALUES ('b@x.org', 'Bob')")
    conn.execute("INSERT INTO courses(title) VALUES ('SQL')")
    conn.execute("INSERT INTO courses(title) VALUES ('Python')")


def raises_integrity(conn, sql, params=()):
    try:
        conn.execute(sql, params)
    except sqlite3.IntegrityError:
        return True
    return False


def t_tables_and_join():
    conn = fresh()
    add_basic(conn)
    conn.execute("INSERT INTO enrollments(student_id, course_id, grade) VALUES (1, 1, 5)")
    conn.execute("INSERT INTO enrollments(student_id, course_id) VALUES (1, 2)")
    rows = conn.execute(
        "SELECT s.name, c.title, e.grade FROM enrollments e "
        "JOIN students s ON s.id = e.student_id JOIN courses c ON c.id = e.course_id ORDER BY c.title"
    ).fetchall()
    if rows != [("Ann", "Python", None), ("Ann", "SQL", 5)]:
        return f"соединение вернуло {rows!r}"


def t_fk_unknown():
    conn = fresh()
    add_basic(conn)
    if not raises_integrity(conn, "INSERT INTO enrollments(student_id, course_id) VALUES (99, 1)"):
        return "запись на курс несуществующего студента должна вызывать IntegrityError (включены ли внешние ключи?)"
    if not raises_integrity(conn, "INSERT INTO enrollments(student_id, course_id) VALUES (1, 99)"):
        return "запись на несуществующий курс должна вызывать IntegrityError"


def t_duplicate():
    conn = fresh()
    add_basic(conn)
    conn.execute("INSERT INTO enrollments(student_id, course_id) VALUES (1, 1)")
    if not raises_integrity(conn, "INSERT INTO enrollments(student_id, course_id) VALUES (1, 1)"):
        return "повторная запись той же пары (student_id, course_id) должна быть отвергнута"
    conn.execute("INSERT INTO enrollments(student_id, course_id) VALUES (2, 1)")


def t_check():
    conn = fresh()
    add_basic(conn)
    for bad in (0, 6, -1):
        if not raises_integrity(conn, "INSERT INTO enrollments(student_id, course_id, grade) VALUES (1, 1, ?)", (bad,)):
            return f"оценка {bad} должна отвергаться ограничением CHECK"
    conn.execute("INSERT INTO enrollments(student_id, course_id, grade) VALUES (1, 1, 1)")
    conn.execute("INSERT INTO enrollments(student_id, course_id, grade) VALUES (1, 2, 5)")
    conn.execute("INSERT INTO enrollments(student_id, course_id, grade) VALUES (2, 1, NULL)")


def t_email():
    conn = fresh()
    conn.execute("INSERT INTO students(email, name) VALUES ('a@x.org', 'Ann')")
    if not raises_integrity(conn, "INSERT INTO students(email, name) VALUES ('a@x.org', 'Other')"):
        return "email должен быть уникальным"
    if not raises_integrity(conn, "INSERT INTO students(email, name) VALUES (NULL, 'NoMail')"):
        return "email обязателен (NOT NULL)"
    if not raises_integrity(conn, "INSERT INTO students(email, name) VALUES ('c@x.org', NULL)"):
        return "name обязательно (NOT NULL)"
    conn.execute("INSERT INTO courses(title) VALUES ('SQL')")
    if not raises_integrity(conn, "INSERT INTO courses(title) VALUES ('SQL')"):
        return "title курса должен быть уникальным"
    if not raises_integrity(conn, "INSERT INTO courses(title) VALUES (NULL)"):
        return "title курса обязателен"


def t_cascade():
    conn = fresh()
    add_basic(conn)
    conn.execute("INSERT INTO enrollments(student_id, course_id) VALUES (1, 1)")
    conn.execute("INSERT INTO enrollments(student_id, course_id) VALUES (1, 2)")
    conn.execute("INSERT INTO enrollments(student_id, course_id) VALUES (2, 1)")
    conn.execute("DELETE FROM students WHERE id = 1")
    left = conn.execute("SELECT student_id, course_id FROM enrollments").fetchall()
    if left != [(2, 1)]:
        return f"после удаления студента 1 должны остаться только его чужие записи, а осталось {left!r} (ON DELETE CASCADE)"


def t_restrict():
    conn = fresh()
    add_basic(conn)
    conn.execute("INSERT INTO enrollments(student_id, course_id) VALUES (1, 1)")
    if not raises_integrity(conn, "DELETE FROM courses WHERE id = 1"):
        return "курс с записями удалять нельзя (ON DELETE RESTRICT или поведение по умолчанию)"
    conn.execute("DELETE FROM courses WHERE id = 2")


def t_pragma_independent():
    a = fresh()
    b = fresh()
    if a.execute("PRAGMA foreign_keys").fetchone() != (1,):
        return "PRAGMA foreign_keys должен быть включён в каждом соединении"
    a.execute("INSERT INTO courses(title) VALUES ('X')")
    if b.execute("SELECT COUNT(*) FROM courses").fetchone() != (0,):
        return "каждый вызов connect() должен создавать независимую базу со своей схемой"
    names = {r[0] for r in a.execute("SELECT name FROM sqlite_master WHERE type = 'table'")}
    if not {"students", "courses", "enrollments"} <= names:
        return f"не хватает таблиц, есть {sorted(names)!r}"


case("tables-and-join", t_tables_and_join)
case("fk-unknown", t_fk_unknown)
case("duplicate-enrollment", t_duplicate)
case("check-grade", t_check)
case("email-not-null-unique", t_email)
case("cascade-delete", t_cascade)
case("restrict-course-delete", t_restrict)
case("connect-is-independent", t_pragma_independent)
