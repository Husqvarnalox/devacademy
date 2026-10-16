import json
import os
import warnings

warnings.filterwarnings("ignore")

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


def expect(actual, expected, what):
    if actual != expected:
        return f"{what}: получено {actual!r}, ожидалось {expected!r}"
    return None


from fastapi.testclient import TestClient

import bank_task as m

client = TestClient(m.app)


def reset():
    m.DB.accounts = {"a": 100, "b": 50}
    m.DB.transactions.clear()


def pay(src, dst, amount):
    return client.post("/transfer", json={"src": src, "dst": dst, "amount": amount})


def t_ok():
    reset()
    r = pay("a", "b", 30)
    bad = expect(r.status_code, 200, "POST /transfer")
    if bad:
        return bad
    bad = expect(m.DB.accounts, {"a": 70, "b": 80}, "балансы после перевода")
    if bad:
        return bad
    tx = m.DB.transactions[-1]
    return expect((tx.committed, tx.rolled_back), (True, False), "состояние транзакции (committed, rolled_back)")


def t_rollback():
    reset()
    r = pay("a", "b", 500)
    bad = expect((r.status_code, r.json()), (409, {"detail": "insufficient funds"}), "перевод больше баланса")
    if bad:
        return bad
    bad = expect(m.DB.accounts, {"a": 100, "b": 50}, "балансы после отказа")
    if bad:
        return bad
    tx = m.DB.transactions[-1]
    bad = expect((tx.committed, tx.rolled_back), (False, True), "транзакция 409: (committed, rolled_back)")
    if bad:
        return bad
    r = pay("a", "zzz", 10)
    bad = expect((r.status_code, r.json()), (404, {"detail": "account not found"}), "неизвестный счёт")
    if bad:
        return bad
    tx = m.DB.transactions[-1]
    return expect((tx.committed, tx.rolled_back), (False, True), "транзакция 404: (committed, rolled_back)")


def t_no_partial():
    reset()
    pay("b", "a", 51)
    bad = expect(m.DB.accounts, {"a": 100, "b": 50}, "после отказа 409 в обратную сторону")
    if bad:
        return bad
    pay("zzz", "a", 1)
    bad = expect(m.DB.accounts, {"a": 100, "b": 50}, "после отказа 404")
    if bad:
        return bad
    pay("a", "b", 100)
    return expect(m.DB.accounts, {"a": 0, "b": 150}, "перевод всего баланса допустим")


def t_amount():
    reset()
    for amount in (0, -5):
        r = pay("a", "b", amount)
        bad = expect(r.status_code, 422, f"amount={amount}")
        if bad:
            return bad
    return expect(m.DB.accounts, {"a": 100, "b": 50}, "балансы после невалидных запросов")


case("transfer-ok", t_ok)
case("errors-rollback", t_rollback)
case("no-partial", t_no_partial)
case("amount-validation", t_amount)
