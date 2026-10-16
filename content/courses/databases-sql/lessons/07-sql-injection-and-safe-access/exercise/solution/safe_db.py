"""Безопасный доступ к базе: только параметры и белые списки."""
import sqlite3

ORDER_COLUMNS = {"name", "price", "category"}


def find_user(conn, name):
    """Вернуть (id, name) пользователя с точным именем или None."""
    return conn.execute("SELECT id, name FROM users WHERE name = ?", (name,)).fetchone()


def search_products(conn, term):
    """Названия товаров, содержащих term буквально (% и _ - обычные символы), по возрастанию."""
    escaped = term.replace("\\", "\\\\").replace("%", "\\%").replace("_", "\\_")
    rows = conn.execute(
        "SELECT name FROM products WHERE name LIKE ? ESCAPE '\\' ORDER BY name",
        (f"%{escaped}%",),
    ).fetchall()
    return [r[0] for r in rows]


def sorted_products(conn, column):
    """Названия товаров, отсортированные по column (name, price или category), затем по id."""
    if column not in ORDER_COLUMNS:
        raise ValueError(f"нельзя сортировать по {column!r}")
    rows = conn.execute(f"SELECT name FROM products ORDER BY {column}, id").fetchall()
    return [r[0] for r in rows]


def products_by_ids(conn, ids):
    """Названия товаров с указанными id, по возрастанию id. Пустой список - []."""
    ids = list(ids)
    if not ids:
        return []
    marks = ", ".join("?" * len(ids))
    rows = conn.execute(
        f"SELECT name FROM products WHERE id IN ({marks}) ORDER BY id", ids
    ).fetchall()
    return [r[0] for r in rows]


def titles_by_author(conn):
    """{имя автора: [названия книг по возрастанию]} для всех авторов, у кого есть книги.
    Должно выполнять не более двух SELECT независимо от числа авторов."""
    result = {}
    rows = conn.execute(
        "SELECT a.name, b.title FROM authors a JOIN books b ON b.author_id = a.id "
        "ORDER BY a.name, b.title"
    ).fetchall()
    for author, title in rows:
        result.setdefault(author, []).append(title)
    return result
