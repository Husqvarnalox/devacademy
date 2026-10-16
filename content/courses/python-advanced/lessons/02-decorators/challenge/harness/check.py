import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() returns None on success or a string that explains the failure."""
    try:
        problem = check()
    except Exception as exc:  # the learner's code may raise anything
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, call):
    if actual != expected:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


import lru_task as m


def make():
    calls = []

    @m.lru(2)
    def sq(x):
        """Квадрат."""
        calls.append(x)
        return x * x

    return sq, calls


def t_basic():
    sq, calls = make()
    got = [sq(2), sq(2), sq(3)]
    bad = expect(got, [4, 4, 9], "результаты")
    if bad:
        return bad
    bad = expect(calls, [2, 3], "вызовы func")
    if bad:
        return bad
    return expect(sq.info(), (1, 2, 2), "info() после sq(2), sq(2), sq(3)")


def t_evict():
    sq, calls = make()
    sq(1)
    sq(2)
    sq(3)
    sq(1)
    return expect(calls, [1, 2, 3, 1], "вызовы func (запись для 1 должна быть вытеснена)")


def t_bad_size():
    for size in (0, -3):
        try:
            m.lru(size)
        except ValueError:
            continue
        return f"lru({size}) должен сразу возбуждать ValueError"


def t_recency():
    sq, calls = make()
    sq(1)
    sq(2)
    sq(1)  # попадание, теперь самая давняя - 2
    sq(3)  # вытесняет 2
    sq(1)  # должно остаться в кеше
    bad = expect(calls, [1, 2, 3], "вызовы func")
    if bad:
        return bad
    sq(2)
    return expect(calls, [1, 2, 3, 2], "вызовы func после sq(2)")


def t_clear_meta():
    sq, calls = make()
    sq(1)
    sq(1)
    sq.clear()
    bad = expect(sq.info(), (0, 0, 0), "info() после clear()")
    if bad:
        return bad
    sq(1)
    bad = expect(calls, [1, 1], "после clear() значение вычисляется заново")
    if bad:
        return bad
    if sq.__name__ != "sq" or sq.__doc__ != "Квадрат.":
        return "не сохранены __name__/__doc__ (functools.wraps)"


def t_size():
    sq, _ = make()
    for i in range(10):
        sq(i)
    h, mi, size = sq.info()
    return expect((h, mi, size), (0, 10, 2), "info() после 10 разных вызовов при maxsize=2")


case("lru-basic", t_basic)
case("lru-evict", t_evict)
case("lru-bad-size", t_bad_size)
case("lru-recency", t_recency)
case("lru-clear-meta", t_clear_meta)
case("lru-size", t_size)
