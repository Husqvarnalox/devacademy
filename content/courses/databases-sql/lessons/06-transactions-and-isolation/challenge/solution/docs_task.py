import sqlite3


class Conflict(Exception):
    """Документ изменили, пока мы его редактировали (версия не совпала)."""


def save(conn, doc_id, body, expected_version):
    cur = conn.execute(
        "UPDATE docs SET body = ?, version = version + 1 WHERE id = ? AND version = ?",
        (body, doc_id, expected_version),
    )
    if cur.rowcount == 1:
        return expected_version + 1
    if conn.execute("SELECT 1 FROM docs WHERE id = ?", (doc_id,)).fetchone() is None:
        raise LookupError(f"нет документа {doc_id}")
    raise Conflict(f"версия документа {doc_id} уже не {expected_version}")


def edit_with_retry(conn, doc_id, transform, max_attempts=3, _after_read=None):
    for _ in range(max_attempts):
        row = conn.execute("SELECT body, version FROM docs WHERE id = ?", (doc_id,)).fetchone()
        if row is None:
            raise LookupError(f"нет документа {doc_id}")
        body, version = row
        if _after_read is not None:
            _after_read()
        try:
            return save(conn, doc_id, transform(body), version)
        except Conflict:
            continue
    raise Conflict(f"не удалось сохранить документ {doc_id} за {max_attempts} попыток")
