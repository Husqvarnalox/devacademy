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


import toolkit_task as m


def t_seq_basic():
    bad = expect(m.diffs([1, 4, 9, 16]), [3, 5, 7], "diffs([1, 4, 9, 16])")
    if bad:
        return bad
    bad = expect(m.diffs([5]), [], "diffs([5])")
    if bad:
        return bad
    bad = expect(m.batches("abcde", 2), [("a", "b"), ("c", "d"), ("e",)], "batches('abcde', 2)")
    if bad:
        return bad
    return expect(m.batches(range(4), 2), [(0, 1), (2, 3)], "batches(range(4), 2)")


def t_combos_basic():
    bad = expect(m.combos("abc", 2), [("a", "b"), ("a", "c"), ("b", "c")], "combos('abc', 2)")
    if bad:
        return bad
    bad = expect(len(m.combos(range(6), 3)), 20, "len(combos(range(6), 3))")
    if bad:
        return bad
    return expect(m.combos([1, 2], 3), [], "combos([1, 2], 3)")


def t_largest_basic():
    bad = expect(m.largest_number([3, 30, 34, 5, 9]), "9534330", "largest_number([3, 30, 34, 5, 9])")
    if bad:
        return bad
    return expect(m.largest_number([10, 2]), "210", "largest_number([10, 2])")


def t_sort_basic():
    recs = [{"n": "b", "a": 2}, {"n": "a", "a": 2}, {"n": "c", "a": 1}]
    got = m.sort_by_fields(recs, "a")
    return expect([r["n"] for r in got], ["c", "b", "a"], "sort_by_fields(recs, 'a')")


def t_dispatch():
    bad = expect(m.to_text(5), "int:5", "to_text(5)")
    if bad:
        return bad
    bad = expect(m.to_text(2.5), "float:2.5", "to_text(2.5)")
    if bad:
        return bad
    bad = expect(m.to_text([1, 2.5, "x"]), "[int:1, float:2.5, 'x']", "to_text([1, 2.5, 'x'])")
    if bad:
        return bad
    bad = expect(m.to_text([[1], []]), "[[int:1], []]", "to_text([[1], []])")
    if bad:
        return bad
    if not hasattr(m.to_text, "register"):
        return "to_text должна быть создана через functools.singledispatch"
    return expect(m.to_text(None), "None", "to_text(None)")


def t_compose():
    inc = lambda x: x + 1
    dbl = lambda x: x * 2
    bad = expect(m.compose(inc, dbl)(5), 11, "compose(inc, dbl)(5)")
    if bad:
        return bad
    bad = expect(m.compose(dbl, inc)(5), 12, "compose(dbl, inc)(5)")
    if bad:
        return bad
    bad = expect(m.compose()(42), 42, "compose()(42)")
    if bad:
        return bad
    return expect(m.compose(str)(7), "7", "compose(str)(7)")


def t_fib():
    bad = expect([m.fib(i) for i in range(8)], [0, 1, 1, 2, 3, 5, 8, 13], "первые числа Фибоначчи")
    if bad:
        return bad
    bad = expect(m.fib(300), 222232244629420445529739893461909967206666939096499764990979600, "fib(300)")
    if bad:
        return bad
    if not hasattr(m.fib, "cache_info"):
        return "у fib нет cache_info: используйте functools.cache или lru_cache"
    if m.fib.cache_info().hits == 0:
        return "кэш не используется (hits == 0)"


def t_edge():
    try:
        m.batches([1, 2], 0)
    except ValueError:
        pass
    else:
        return "batches(..., 0) должен поднимать ValueError"
    bad = expect(m.largest_number([0, 0]), "0", "largest_number([0, 0])")
    if bad:
        return bad
    bad = expect(m.largest_number([]), "", "largest_number([])")
    if bad:
        return bad
    bad = expect(m.largest_number([824, 8247]), "8248247", "largest_number([824, 8247])")
    if bad:
        return bad
    recs = [{"c": "x", "n": 2}, {"c": "a", "n": 2}, {"c": "x", "n": 1}, {"c": "a", "n": 2, "id": 2}]
    snapshot = list(recs)
    got = m.sort_by_fields(recs, "c", "n")
    bad = expect(got, [recs[1], recs[3], recs[2], recs[0]], "sort_by_fields по двум полям")
    if bad:
        return bad
    if recs != snapshot or got is recs:
        return "sort_by_fields не должен менять исходный список"


case("seq-basic", t_seq_basic)
case("combos-basic", t_combos_basic)
case("largest-basic", t_largest_basic)
case("sort-basic", t_sort_basic)
case("dispatch-basic", t_dispatch)
case("compose-reduce", t_compose)
case("fib-cache", t_fib)
case("edge-cases", t_edge)
