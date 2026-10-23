import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import syntax_task as m


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


def t_wrap_basic():
    for i, n, want in [(0, 5, 0), (3, 5, 3), (-1, 5, 4)]:
        bad = expect(m.wrap_index(i, n), want, f"wrap_index({i}, {n})")
        if bad:
            return bad


def t_wrap_far():
    for i, n, want in [(5, 5, 0), (12, 5, 2), (-13, 5, 2), (-5, 5, 0), (-1, 1, 0)]:
        bad = expect(m.wrap_index(i, n), want, f"wrap_index({i}, {n})")
        if bad:
            return bad


def t_present_basic():
    bad = expect(m.first_present([None, "", "a", "b"]), "a", "first_present([None, '', 'a', 'b'])")
    return bad or expect(m.first_present(["x"]), "x", "first_present(['x'])")


def t_present_falsy():
    for values, want in [([None, "", 0, 7], 0), ([None, False], False), (["", [], 3], [])]:
        got = m.first_present(values)
        if got != want or type(got) is not type(want):
            return f"first_present({values!r}) вернуло {got!r}, ожидалось {want!r}"
    bad = expect(m.first_present([None, ""], "нет"), "нет", "first_present([None, ''], 'нет')")
    return bad or expect(m.first_present([]), None, "first_present([])")


def t_fact_basic():
    for n, want in [(10, 27), (5, 3), (3, 6)]:
        bad = expect(m.factorial_digit_sum(n), want, f"factorial_digit_sum({n})")
        if bad:
            return bad


def t_fact_big():
    for n, want in [(0, 1), (1, 1), (100, 648), (500, 4599)]:
        bad = expect(m.factorial_digit_sum(n), want, f"factorial_digit_sum({n})")
        if bad:
            return bad


def t_deltas_basic():
    bad = expect(m.deltas([3, 8, 6]), [5, -2], "deltas([3, 8, 6])")
    return bad or expect(m.deltas([1, 2, 4, 8]), [1, 2, 4], "deltas([1, 2, 4, 8])")


def t_deltas_edges():
    for values, want in [([], []), ([5], []), ((1, 1, 1), [0, 0]), ([2.5, 1.0], [-1.5])]:
        got = m.deltas(values)
        if not isinstance(got, list) or got != want:
            return f"deltas({values!r}) вернуло {got!r}, ожидался список {want!r}"
    original = [1, 4, 9]
    m.deltas(original)
    return expect(original, [1, 4, 9], "исходный список после deltas")


def t_numbered_basic():
    return expect(m.numbered(["чай", "кофе"]), ["1. чай", "2. кофе"], "numbered(['чай', 'кофе'])")


def t_numbered_start():
    bad = expect(m.numbered(["a", "b", "c"], 10), ["10. a", "11. b", "12. c"], "numbered(['a', 'b', 'c'], 10)")
    return bad or expect(m.numbered([]), [], "numbered([])")


case("wrap-basic", t_wrap_basic)
case("first-present-basic", t_present_basic)
case("factorial-basic", t_fact_basic)
case("deltas-basic", t_deltas_basic)
case("numbered-basic", t_numbered_basic)
case("wrap-negative-far", t_wrap_far)
case("first-present-falsy", t_present_falsy)
case("factorial-big", t_fact_big)
case("deltas-edges", t_deltas_edges)
case("numbered-start", t_numbered_start)
