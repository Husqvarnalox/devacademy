import sqlite3

# Таблицы (создаёт проверяющий код):
#   accounts(id INTEGER PRIMARY KEY, owner TEXT, balance INTEGER NOT NULL CHECK (balance >= 0))
#   transfers(key TEXT PRIMARY KEY, src INTEGER, dst INTEGER, amount INTEGER)
#
# Соединение conn открыто как sqlite3.connect(..., autocommit=True): неявных транзакций нет,
# conn.commit() и conn.rollback() в этом режиме НИЧЕГО не делают. Управляйте транзакцией
# операторами BEGIN IMMEDIATE / COMMIT / ROLLBACK через conn.execute.


class InsufficientFunds(Exception):
    """На счёте-источнике не хватает средств."""


def transfer(conn, src, dst, amount):
    """Атомарно перевести amount со счёта src на счёт dst.

    * amount <= 0 или src == dst -> ValueError (до обращения к базе);
    * нет счёта src или dst -> LookupError;
    * не хватает денег (нарушение CHECK) -> InsufficientFunds;
    * при любой ошибке в базе не остаётся никаких изменений, а транзакция закрыта.
    """
    conn.execute("UPDATE accounts SET balance = balance - ? WHERE id = ?", (amount, src))
    conn.execute("UPDATE accounts SET balance = balance + ? WHERE id = ?", (amount, dst))


def transfer_once(conn, key, src, dst, amount):
    """Идемпотентный перевод по ключу key.

    Первый успешный вызов с данным key выполняет перевод и возвращает True.
    Повтор с тем же key ничего не меняет и возвращает False.
    Если перевод не удался, ключ НЕ запоминается (повтор после исправления причины сработает).
    Запись в transfers и перевод - в одной транзакции.
    """
    transfer(conn, src, dst, amount)
    return True
