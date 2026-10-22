import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import iterators_check as m


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


import itertools


def is_iterator(obj):
    try:
        return iter(obj) is obj
    except TypeError:
        return False


def tracked(source, pulled):
    for item in source:
        pulled.append(item)
        yield item


def t_parse_basic():
    got = m.parse_ints(["1", "  ", "-2", "30\n", ""])
    if not is_iterator(got):
        return "parse_ints должна возвращать итератор (используйте yield)"
    return expect(list(got), [1, -2, 30], "list(parse_ints(['1', '  ', '-2', '30\\n', '']))")


def t_window_basic():
    got = m.window("abcd", 2)
    if not is_iterator(got):
        return "window должна возвращать итератор (используйте yield)"
    bad = expect(list(got), [("a", "b"), ("b", "c"), ("c", "d")], "list(window('abcd', 2))")
    return bad or expect(list(m.window([1, 2, 3, 4, 5], 3)), [(1, 2, 3), (2, 3, 4), (3, 4, 5)], "list(window([1, 2, 3, 4, 5], 3))")


def t_running_basic():
    got = m.running_total([1, 2, 3])
    if not is_iterator(got):
        return "running_total должна возвращать итератор (используйте yield)"
    return expect(list(got), [1, 3, 6], "list(running_total([1, 2, 3]))")


def t_window_lazy():
    first = list(itertools.islice(m.window(itertools.count(), 3), 2))
    bad = expect(first, [(0, 1, 2), (1, 2, 3)], "первые два окна window(count(), 3)")
    if bad:
        return bad
    pulled = []
    gen = m.window(tracked(range(100), pulled), 3)
    next(gen)
    return expect(len(pulled), 3, "сколько элементов источника прочитано ради первого окна")


def t_running_lazy():
    first = list(itertools.islice(m.running_total(itertools.count()), 5))
    bad = expect(first, [0, 1, 3, 6, 10], "первые пять значений running_total(count())")
    if bad:
        return bad
    pulled = []
    gen = m.running_total(tracked(range(100), pulled))
    next(gen)
    next(gen)
    return expect(len(pulled), 2, "сколько элементов источника прочитано ради двух значений")


def t_window_edges():
    bad = expect(list(m.window([1, 2], 3)), [], "window([1, 2], 3)")
    bad = bad or expect(list(m.window([], 2)), [], "window([], 2)")
    bad = bad or expect(list(m.window([7, 8], 1)), [(7,), (8,)], "window([7, 8], 1)")
    bad = bad or expect(list(m.window(iter("xyz"), 2)), [("x", "y"), ("y", "z")], "window(итератор, 2)")
    bad = bad or expect(list(m.running_total([])), [], "running_total([])")
    return bad or expect(list(m.running_total([2.5, -1])), [2.5, 1.5], "running_total([2.5, -1])")


def t_pipeline():
    got = list(m.running_total(m.parse_ints(["1", " ", "2", "3\n"])))
    bad = expect(got, [1, 3, 6], "конвейер running_total(parse_ints(...))")
    sums = list(m.running_total(x[0] + x[1] for x in m.window(m.parse_ints(iter(["1", "2", "", "4"])), 2)))
    return bad or expect(sums, [3, 9], "конвейер с window и генераторным выражением")


case("parse-basic", t_parse_basic)
case("window-basic", t_window_basic)
case("running-basic", t_running_basic)
case("window-lazy", t_window_lazy)
case("running-lazy", t_running_lazy)
case("window-edges", t_window_edges)
case("pipeline", t_pipeline)
