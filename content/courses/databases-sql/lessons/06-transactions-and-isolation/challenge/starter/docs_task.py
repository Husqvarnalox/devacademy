import sqlite3

# Таблица (создаёт проверяющий код):
#   docs(id INTEGER PRIMARY KEY, body TEXT NOT NULL, version INTEGER NOT NULL)
# Соединение открыто с autocommit=True.


class Conflict(Exception):
    """Документ изменили, пока мы его редактировали (версия не совпала)."""


def save(conn, doc_id, body, expected_version):
    """Оптимистичная блокировка: сохранить body, только если версия документа равна expected_version.

    Успех: body обновлён, version увеличена на 1, возвращается новая версия.
    Версия не совпала -> Conflict (документ не изменён).
    Документа нет -> LookupError.
    Проверка и запись - ОДИН оператор UPDATE (без отдельного SELECT перед ним).
    """
    conn.execute("UPDATE docs SET body = ? WHERE id = ?", (body, doc_id))
    return expected_version + 1


def edit_with_retry(conn, doc_id, transform, max_attempts=3, _after_read=None):
    """Прочитать документ, применить transform(body) -> новый текст и сохранить через save.

    При Conflict перечитать документ и повторить с НОВЫМ текстом (transform вызывается заново).
    После max_attempts неудачных попыток поднять Conflict. Вернуть новую версию.
    Хук _after_read() (если задан) вызывается после чтения и до сохранения: проверяющий код
    использует его, чтобы имитировать конкурентную запись.
    """
    row = conn.execute("SELECT body, version FROM docs WHERE id = ?", (doc_id,)).fetchone()
    return save(conn, doc_id, transform(row[0]), row[1])
