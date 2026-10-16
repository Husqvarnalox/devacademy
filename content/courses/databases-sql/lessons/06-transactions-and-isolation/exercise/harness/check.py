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


import bank as m


def fresh():
    conn = sqlite3.connect(":memory:", autocommit=True)
    conn.executescript(
        "CREATE TABLE accounts(id INTEGER PRIMARY KEY, owner TEXT, balance INTEGER NOT NULL CHECK (balance >= 0));"
        "CREATE TABLE transfers(key TEXT PRIMARY KEY, src INTEGER, dst INTEGER, amount INTEGER);"
        "INSERT INTO accounts VALUES (1, 'Ann', 100), (2, 'Bob', 50), (3, 'Cy', 0);"
    )
    return conn


def balances(conn):
    return [r[0] for r in conn.execute("SELECT balance FROM accounts ORDER BY id")]


def closed(conn):
    if conn.in_transaction:
        return "после вызова транзакция осталась открытой (в режиме autocommit=True conn.commit() ничего не делает - выполните COMMIT или ROLLBACK оператором)"


def t_ok():
    conn = fresh()
    m.transfer(conn, 1, 2, 30)
    if balances(conn) != [70, 80, 0]:
        return f"балансы {balances(conn)!r}, ожидалось [70, 80, 0]"
    return closed(conn)


def t_insufficient():
    conn = fresh()
    try:
        m.transfer(conn, 3, 1, 10)
    except m.InsufficientFunds:
        pass
    else:
        return "ожидалось исключение InsufficientFunds"
    if balances(conn) != [100, 50, 0]:
        return f"после неудачного перевода балансы {balances(conn)!r}, ожидалось [100, 50, 0]"
    return closed(conn)


def t_idempotent():
    conn = fresh()
    first = m.transfer_once(conn, "k1", 1, 2, 40)
    second = m.transfer_once(conn, "k1", 1, 2, 40)
    if first is not True or second is not False:
        return f"transfer_once вернула {first!r}, затем {second!r}; ожидалось True, затем False"
    if balances(conn) != [60, 90, 0]:
        return f"после повтора балансы {balances(conn)!r}, ожидалось [60, 90, 0] (перевод выполнен один раз)"
    return closed(conn)


def t_unknown_dst():
    conn = fresh()
    try:
        m.transfer(conn, 1, 99, 10)
    except LookupError:
        pass
    else:
        return "ожидалось LookupError для несуществующего счёта получателя"
    if balances(conn) != [100, 50, 0]:
        return f"списание не откатилось: балансы {balances(conn)!r}"
    try:
        m.transfer(conn, 99, 1, 10)
    except LookupError:
        pass
    else:
        return "ожидалось LookupError для несуществующего счёта источника"
    if balances(conn) != [100, 50, 0]:
        return f"балансы изменились: {balances(conn)!r}"
    return closed(conn)


def t_validation():
    conn = fresh()
    for args in [(1, 2, 0), (1, 2, -5), (1, 1, 10)]:
        try:
            m.transfer(conn, *args)
        except ValueError:
            pass
        else:
            return f"transfer{args} должна поднимать ValueError"
        try:
            m.transfer_once(conn, "kv", *args)
        except ValueError:
            pass
        else:
            return f"transfer_once('kv', *{args}) должна поднимать ValueError"
    if balances(conn) != [100, 50, 0] or conn.execute("SELECT COUNT(*) FROM transfers").fetchone()[0] != 0:
        return "некорректные вызовы не должны ничего менять и ничего записывать в transfers"
    return closed(conn)


def t_failed_key():
    conn = fresh()
    try:
        m.transfer_once(conn, "kf", 3, 1, 25)
    except m.InsufficientFunds:
        pass
    else:
        return "ожидалось InsufficientFunds"
    if conn.execute("SELECT COUNT(*) FROM transfers").fetchone()[0] != 0:
        return "ключ неудавшегося перевода не должен сохраняться (запись в transfers должна откатываться вместе с переводом)"
    m.transfer(conn, 1, 3, 30)
    if m.transfer_once(conn, "kf", 3, 1, 25) is not True:
        return "после пополнения повтор с тем же ключом должен выполниться и вернуть True"
    if balances(conn) != [95, 50, 5]:
        return f"балансы {balances(conn)!r}, ожидалось [95, 50, 5]"
    return closed(conn)


def t_series():
    conn = fresh()
    ops = [(1, 2, 20), (2, 3, 80), (3, 1, 500), (2, 99, 1), (3, 2, 10), (1, 3, 100), (2, 1, 5), (1, 2, 7)]
    for i, (s, d, a) in enumerate(ops):
        try:
            if i % 2:
                m.transfer_once(conn, f"op{i}", s, d, a)
                m.transfer_once(conn, f"op{i}", s, d, a)
            else:
                m.transfer(conn, s, d, a)
        except (m.InsufficientFunds, LookupError):
            pass
        if sum(balances(conn)) != 150 or min(balances(conn)) < 0:
            return f"после операции {i} {ops[i]}: балансы {balances(conn)!r} нарушают сохранение денег"
        bad = closed(conn)
        if bad:
            return bad
    expected = [100, 50, 0]
    # независимый расчёт ожидаемого результата
    state = {1: 100, 2: 50, 3: 0}
    seen = set()
    for i, (s, d, a) in enumerate(ops):
        if d not in state:
            continue
        if i % 2:
            if f"op{i}" in seen:
                continue
        if state[s] >= a:
            state[s] -= a
            state[d] += a
            if i % 2:
                seen.add(f"op{i}")
    expected = [state[1], state[2], state[3]]
    if balances(conn) != expected:
        return f"итоговые балансы {balances(conn)!r}, ожидалось {expected!r}"


case("transfer-ok", t_ok)
case("insufficient-rollback", t_insufficient)
case("idempotent-retry", t_idempotent)
case("unknown-account-rollback", t_unknown_dst)
case("validation", t_validation)
case("failed-key-not-recorded", t_failed_key)
case("series-conservation", t_series)
