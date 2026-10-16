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
import time

import prio_task as m


def t_topk_basic():
    bad = expect(m.top_k([5, 1, 9, 3], 2), [9, 5], "top_k([5, 1, 9, 3], 2)")
    if bad:
        return bad
    words = ["pear", "fig", "banana", "kiwi"]
    return expect(m.top_k(words, 2, key=len), ["banana", "pear"], "top_k(words, 2, key=len)")


def t_topk_edge():
    bad = expect(m.top_k([3, 1, 2], 10), [3, 2, 1], "top_k([3, 1, 2], 10)")
    if bad:
        return bad
    bad = expect(m.top_k([3, 1], 0), [], "top_k(..., 0)")
    if bad:
        return bad
    bad = expect(m.top_k([3, 1], -2), [], "top_k(..., -2)")
    if bad:
        return bad
    bad = expect(m.top_k(["bb", "aa", "c", "dd"], 2, key=len), ["bb", "aa"], "ничья по ключу: порядок входа")
    if bad:
        return bad
    return expect(m.top_k(iter([4, 8, 6]), 2), [8, 6], "top_k(iter(...), 2)")


def t_queue_basic():
    q = m.TaskQueue()
    q.push(3, "c")
    q.push(1, "a")
    q.push(2, "b")
    bad = expect(len(q), 3, "len(q)")
    if bad:
        return bad
    return expect([q.pop(), q.pop(), q.pop()], ["a", "b", "c"], "три pop()")


def t_queue_ties_unorderable():
    q = m.TaskQueue()
    a, b, c = {"n": 1}, {"n": 2}, {"n": 3}
    q.push(1, a)
    q.push(1, b)
    q.push(0, c)
    q.push(1, {"n": 4})
    got = [q.pop()["n"] for _ in range(4)]
    return expect(got, [3, 1, 2, 4], "порядок при равных приоритетах и dict-элементах")


def t_queue_empty():
    q = m.TaskQueue()
    for name in ("pop", "peek"):
        try:
            getattr(q, name)()
        except IndexError:
            continue
        return f"{name}() пустой очереди должен поднимать IndexError"
    q.push(2, "x")
    q.push(1, "y")
    bad = expect((q.peek(), len(q)), ("y", 2), "peek() не удаляет")
    if bad:
        return bad
    q.pop()
    return expect(q.peek(), "x", "peek() после pop()")


def t_schedule_basic():
    got = m.schedule([(0, 2, "a"), (0, 1, "b"), (5, 1, "c")])
    return expect(got, [(0, "b"), (1, "a"), (5, "c")], "schedule(...)")


def t_schedule_gaps():
    tasks = [(10, 1, "late"), (2, 5, "x"), (2, 5, "y"), (3, 1, "hot"), (3, 5, "z")]
    expected = [(2, "x"), (3, "hot"), (4, "y"), (5, "z"), (10, "late")]
    bad = expect(m.schedule(tasks), expected, "schedule с простоем и ничьими")
    if bad:
        return bad
    return expect(m.schedule([]), [], "schedule([])")


def t_schedule_big():
    rnd = random.Random(7)
    tasks = [(rnd.randrange(0, 20000), rnd.randrange(0, 50), f"t{i}") for i in range(50_000)]
    start = time.perf_counter()
    got = m.schedule(tasks)
    elapsed = time.perf_counter() - start
    if len(got) != len(tasks):
        return f"выполнено {len(got)} задач из {len(tasks)}"
    times = [t for t, _ in got]
    if any(b <= a for a, b in zip(times, times[1:])):
        return "времена старта должны строго возрастать"
    if elapsed > 5:
        return f"слишком медленно ({elapsed:.1f} с): нужна куча, а не линейный поиск минимума"


def t_range_basic():
    bad = expect(m.count_in_range([1, 2, 2, 5, 7], 2, 7), 3, "count_in_range([1,2,2,5,7], 2, 7)")
    if bad:
        return bad
    return expect(m.count_in_range([1, 3, 5], 0, 100), 3, "count_in_range([1,3,5], 0, 100)")


def t_range_edge():
    bad = expect(m.count_in_range([1, 2, 2, 2, 5], 2, 3), 3, "дубликаты")
    if bad:
        return bad
    bad = expect(m.count_in_range([1, 2, 3], 5, 2), 0, "hi < lo")
    if bad:
        return bad
    bad = expect(m.count_in_range([1, 2, 3], 2, 2), 0, "hi == lo")
    if bad:
        return bad
    bad = expect(m.count_in_range([], 0, 10), 0, "пустой список")
    if bad:
        return bad
    return expect(m.count_in_range([10, 20], -5, 5), 0, "диапазон левее данных")


case("topk-basic", t_topk_basic)
case("queue-basic", t_queue_basic)
case("schedule-basic", t_schedule_basic)
case("range-basic", t_range_basic)
case("topk-edge", t_topk_edge)
case("queue-ties-unorderable", t_queue_ties_unorderable)
case("queue-empty", t_queue_empty)
case("schedule-gaps", t_schedule_gaps)
case("range-edge", t_range_edge)
case("schedule-big", t_schedule_big)
