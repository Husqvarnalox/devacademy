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


import time

import lru_task as m


def t_basic():
    c = m.LRUCache(2)
    c.put("a", 1)
    c.put("b", 2)
    c.put("c", 3)
    bad = expect(c.keys(), ["b", "c"], "keys() после трёх put в кэш ёмкости 2")
    if bad:
        return bad
    bad = expect(c.get("a"), None, "get('a') вытесненного ключа")
    if bad:
        return bad
    bad = expect(c.get("a", -1), -1, "get('a', -1)")
    if bad:
        return bad
    return expect(len(c), 2, "len(c)")


def t_get_refresh():
    c = m.LRUCache(2)
    c.put("a", 1)
    c.put("b", 2)
    bad = expect(c.get("a"), 1, "get('a')")
    if bad:
        return bad
    c.put("c", 3)
    return expect(c.keys(), ["a", "c"], "keys() после get('a') и put('c')")


def t_update():
    c = m.LRUCache(2)
    c.put("a", 1)
    c.put("b", 2)
    c.put("a", 10)
    bad = expect((len(c), c.keys(), c.get("a")), (2, ["b", "a"], 10), "состояние после обновления 'a'")
    if bad:
        return bad
    c.put("c", 3)
    return expect(c.keys(), ["a", "c"], "keys() после put('c')")


def t_contains_pure():
    c = m.LRUCache(2)
    c.put("a", 1)
    c.put("b", 2)
    if "a" not in c or "z" in c:
        return "оператор in работает неверно"
    c.keys()
    c.put("c", 3)
    return expect(c.keys(), ["b", "c"], "keys() - in и keys() не должны освежать ключ 'a'")


def t_capacity_error():
    for cap in (0, -3):
        try:
            m.LRUCache(cap)
        except ValueError:
            continue
        return f"LRUCache({cap}) должен поднимать ValueError"
    c = m.LRUCache(1)
    c.put(1, "x")
    c.put(2, "y")
    return expect(c.keys(), [2], "ёмкость 1")


def t_big():
    c = m.LRUCache(1000)
    start = time.perf_counter()
    for i in range(200_000):
        c.put(i % 1500, i)
        c.get((i * 7) % 1500)
    if len(c) != 1000:
        return f"len(c) == {len(c)}, ожидалось 1000"
    if time.perf_counter() - start > 5:
        return "слишком медленно: get/put должны быть O(1)"


case("lru-basic", t_basic)
case("lru-get-refresh", t_get_refresh)
case("lru-update", t_update)
case("lru-contains-pure", t_contains_pure)
case("lru-capacity-error", t_capacity_error)
case("lru-big", t_big)
