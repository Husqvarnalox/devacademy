import sqlite3


class InsufficientFunds(Exception):
    """На счёте-источнике не хватает средств."""


def _validate(src, dst, amount):
    if amount <= 0 or src == dst:
        raise ValueError("сумма должна быть положительной, счета - разными")


def _move(conn, src, dst, amount):
    try:
        cur = conn.execute("UPDATE accounts SET balance = balance - ? WHERE id = ?", (amount, src))
    except sqlite3.IntegrityError as exc:
        raise InsufficientFunds(f"на счёте {src} не хватает {amount}") from exc
    if cur.rowcount != 1:
        raise LookupError(f"нет счёта {src}")
    cur = conn.execute("UPDATE accounts SET balance = balance + ? WHERE id = ?", (amount, dst))
    if cur.rowcount != 1:
        raise LookupError(f"нет счёта {dst}")


def transfer(conn, src, dst, amount):
    _validate(src, dst, amount)
    conn.execute("BEGIN IMMEDIATE")
    try:
        _move(conn, src, dst, amount)
    except BaseException:
        conn.execute("ROLLBACK")
        raise
    conn.execute("COMMIT")


def transfer_once(conn, key, src, dst, amount):
    _validate(src, dst, amount)
    conn.execute("BEGIN IMMEDIATE")
    try:
        try:
            conn.execute("INSERT INTO transfers(key, src, dst, amount) VALUES (?, ?, ?, ?)", (key, src, dst, amount))
        except sqlite3.IntegrityError:
            conn.execute("ROLLBACK")
            return False
        _move(conn, src, dst, amount)
    except BaseException:
        if conn.in_transaction:
            conn.execute("ROLLBACK")
        raise
    conn.execute("COMMIT")
    return True
