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


import random
import statistics
import time

import median_task as m


def t_basic():
    r = m.RunningMedian()
    got = []
    for x in [5, 15, 1, 3]:
        r.add(x)
        got.append(r.median())
    bad = expect(got, [5, 10.0, 5, 4.0], "медианы после 5, 15, 1, 3")
    if bad:
        return bad
    return expect(len(r), 4, "len(r)")


def t_empty():
    try:
        m.RunningMedian().median()
    except ValueError:
        return None
    return "median() пустого потока должен поднимать ValueError"


def t_negative():
    r = m.RunningMedian()
    data = [-5, 0, -5, 3, 0, -1]
    seen = []
    for x in data:
        r.add(x)
        seen.append(x)
        bad = expect(r.median(), statistics.median(seen), f"median() после {seen}")
        if bad:
            return bad


def t_random():
    rnd = random.Random(2024)
    r = m.RunningMedian()
    seen = []
    for _ in range(300):
        x = rnd.randint(-50, 50)
        r.add(x)
        seen.append(x)
        bad = expect(r.median(), statistics.median(seen), f"median() после {len(seen)} чисел")
        if bad:
            return bad


def t_fast():
    rnd = random.Random(5)
    r = m.RunningMedian()
    start = time.perf_counter()
    for _ in range(200_000):
        r.add(rnd.random())
        r.median()
    if time.perf_counter() - start > 5:
        return "слишком медленно: add должен быть O(log n)"


case("median-basic", t_basic)
case("median-empty", t_empty)
case("median-negative", t_negative)
case("median-random", t_random)
case("median-fast", t_fast)
