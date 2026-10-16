"""Небезопасный доступ к базе. Исправьте все функции."""
import sqlite3

ORDER_COLUMNS = {"name", "price", "category"}


def find_user(conn, name):
    """Вернуть (id, name) пользователя с точным именем или None."""
    # Уязвимо: значение вставлено в текст запроса.
    return conn.execute(f"SELECT id, name FROM users WHERE name = '{name}'").fetchone()


def search_products(conn, term):
    """Названия товаров, содержащих term буквально (% и _ - обычные символы), по возрастанию."""
    rows = conn.execute(f"SELECT name FROM products WHERE name LIKE '%{term}%' ORDER BY name").fetchall()
    return [r[0] for r in rows]


def sorted_products(conn, column):
    """Названия товаров, отсортированные по column (name, price или category), затем по id.
    Другое имя столбца - ValueError."""
    rows = conn.execute(f"SELECT name FROM products ORDER BY {column}, id").fetchall()
    return [r[0] for r in rows]


def products_by_ids(conn, ids):
    """Названия товаров с указанными id, по возрастанию id. Пустой список - []."""
    id_list = ",".join(str(i) for i in ids)
    rows = conn.execute(f"SELECT name FROM products WHERE id IN ({id_list}) ORDER BY id").fetchall()
    return [r[0] for r in rows]


def titles_by_author(conn):
    """{имя автора: [названия книг по возрастанию]} для всех авторов, у кого есть книги.
    Должно выполнять не более двух SELECT независимо от числа авторов."""
    result = {}
    for author_id, name in conn.execute("SELECT id, name FROM authors ORDER BY name").fetchall():
        rows = conn.execute(f"SELECT title FROM books WHERE author_id = {author_id} ORDER BY title").fetchall()
        if rows:
            result[name] = [r[0] for r in rows]
    return result
