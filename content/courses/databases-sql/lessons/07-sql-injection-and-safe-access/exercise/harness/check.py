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


import safe_db


def make_db():
    conn = sqlite3.connect(":memory:")
    conn.executescript(
        """
        CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL, secret TEXT NOT NULL);
        INSERT INTO users VALUES (1, 'alice', 's1'), (2, 'bob', 's2'), (3, 'O''Brien', 's3');
        CREATE TABLE products (id INTEGER PRIMARY KEY, name TEXT NOT NULL, price INTEGER NOT NULL, category TEXT NOT NULL);
        INSERT INTO products VALUES
          (1, 'Cable 100% cotton', 30, 'home'),
          (2, 'Cable_USB', 10, 'tech'),
          (3, 'Cable USB-C', 20, 'tech'),
          (4, 'Lamp', 50, 'home'),
          (5, 'Mouse', 20, 'tech');
        CREATE TABLE authors (id INTEGER PRIMARY KEY, name TEXT NOT NULL);
        INSERT INTO authors VALUES (1, 'Tolstoy'), (2, 'Chekhov'), (3, 'Nobody'), (4, 'Gogol');
        CREATE TABLE books (id INTEGER PRIMARY KEY, author_id INTEGER NOT NULL, title TEXT NOT NULL);
        INSERT INTO books VALUES (1, 1, 'War and Peace'), (2, 1, 'Anna Karenina'),
          (3, 2, 'The Seagull'), (4, 4, 'Dead Souls'), (5, 4, 'The Nose'), (6, 4, 'The Overcoat');
        """
    )
    return conn


def t_find_user():
    c = make_db()
    r = safe_db.find_user(c, "alice")
    if tuple(r or ()) != (1, "alice"):
        return f"ожидали (1, 'alice'), получили {r!r}"
    if safe_db.find_user(c, "nobody") is not None:
        return "несуществующий пользователь должен давать None"


def t_quote():
    c = make_db()
    r = safe_db.find_user(c, "O'Brien")
    if tuple(r or ()) != (3, "O'Brien"):
        return f"имя с апострофом: ожидали (3, \"O'Brien\"), получили {r!r}"


def count_selects(func):
    c = make_db()
    seen = []
    c.set_trace_callback(lambda s: seen.append(s))
    result = func(c)
    n = sum(1 for s in seen if s.lstrip().upper().startswith("SELECT"))
    return result, n


def t_n_plus_one():
    result, n = count_selects(safe_db.titles_by_author)
    expected = {
        "Chekhov": ["The Seagull"],
        "Gogol": ["Dead Souls", "The Nose", "The Overcoat"],
        "Tolstoy": ["Anna Karenina", "War and Peace"],
    }
    if result != expected:
        return f"неверный результат: {result!r}"
    if n > 2:
        return f"выполнено {n} запросов SELECT, допустимо не более 2 (проблема N+1)"


def t_injection():
    c = make_db()
    for payload in ("' OR '1'='1", "x' OR 1=1 --", "alice' --", "'; DROP TABLE users; --"):
        r = safe_db.find_user(c, payload)
        if r is not None:
            return f"строка {payload!r} должна быть просто именем, а вернулось {r!r}"
    if c.execute("SELECT count(*) FROM users").fetchone()[0] != 3:
        return "таблица users изменилась"


def t_like():
    c = make_db()
    cases = {
        "%": ["Cable 100% cotton"],
        "_": ["Cable_USB"],
        "Cable": ["Cable 100% cotton", "Cable USB-C", "Cable_USB"],
        "100%": ["Cable 100% cotton"],
        "e_U": ["Cable_USB"],
        "x": [],
        "'": [],
    }
    for term, want in cases.items():
        got = safe_db.search_products(c, term)
        if got != want:
            return f"search_products({term!r}): ожидали {want!r}, получили {got!r}"
    got = safe_db.search_products(c, "\\")
    if got != []:
        return f"обратная косая черта - обычный символ: ожидали [], получили {got!r}"


def t_order():
    c = make_db()
    cases = {
        "name": ["Cable 100% cotton", "Cable USB-C", "Cable_USB", "Lamp", "Mouse"],
        "price": ["Cable_USB", "Cable USB-C", "Mouse", "Cable 100% cotton", "Lamp"],
        "category": ["Cable 100% cotton", "Lamp", "Cable_USB", "Cable USB-C", "Mouse"],
    }
    for col, want in cases.items():
        got = safe_db.sorted_products(c, col)
        if got != want:
            return f"sorted_products({col!r}): ожидали {want!r}, получили {got!r}"
    for bad in ("secret", "price; DROP TABLE products", "name, (SELECT secret FROM users)", "", "NAME"):
        try:
            safe_db.sorted_products(c, bad)
        except ValueError:
            continue
        return f"столбец {bad!r} вне белого списка должен давать ValueError"
    if c.execute("SELECT count(*) FROM products").fetchone()[0] != 5:
        return "таблица products изменилась"


def t_in():
    c = make_db()
    if safe_db.products_by_ids(c, [3, 1]) != ["Cable 100% cotton", "Cable USB-C"]:
        return f"[3, 1]: получили {safe_db.products_by_ids(c, [3, 1])!r}"
    if safe_db.products_by_ids(c, []) != []:
        return "пустой список id должен давать []"
    if safe_db.products_by_ids(c, (5,)) != ["Mouse"]:
        return "кортеж из одного id"
    if safe_db.products_by_ids(c, [99]) != []:
        return "несуществующий id"
    try:
        r = safe_db.products_by_ids(c, ["1) OR (1=1"])
    except sqlite3.Error:
        r = []
    if r:
        return f"значение-строка не должно становиться частью SQL, а вернулось {r!r}"


case("find-user", t_find_user)
case("quote-safe", t_quote)
case("n-plus-one", t_n_plus_one)
case("injection-returns-nothing", t_injection)
case("like-escape", t_like)
case("order-allowlist", t_order)
case("in-clause", t_in)
