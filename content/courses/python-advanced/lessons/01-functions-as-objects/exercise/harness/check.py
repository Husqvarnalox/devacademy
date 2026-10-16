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


import functional_task as m


def t_compose_basic():
    bad = expect(m.compose(str, abs)(-3), "3", "compose(str, abs)(-3)")
    if bad:
        return bad
    f = m.compose(lambda x: x + 1, lambda x: x * 2)
    bad = expect(f(5), 11, "compose(x+1, x*2)(5)")
    if bad:
        return bad
    return expect(m.compose(len)("abcd"), 4, "compose(len)('abcd')")


def t_compose_identity():
    f = m.compose()
    for v in (5, "x", None):
        bad = expect(f(v), v, f"compose()({v!r})")
        if bad:
            return bad


def t_counter_basic():
    c = m.make_counter()
    got = [c(), c(), c()]
    bad = expect(got, [1, 2, 3], "три вызова make_counter()")
    if bad:
        return bad
    d = m.make_counter(10)
    return expect([d(), d()], [11, 12], "два вызова make_counter(10)")


def t_counter_independent():
    a = m.make_counter()
    b = m.make_counter(100)
    a()
    a()
    got = [b(), a(), b()]
    return expect(got, [101, 3, 102], "чередующиеся вызовы двух счётчиков")


def t_multipliers_basic():
    fs = m.make_multipliers(4)
    if len(fs) != 4:
        return f"ожидалось 4 функции, получено {len(fs)}"
    return expect([f(5) for f in fs], [0, 5, 10, 15], "[f(5) for f in make_multipliers(4)]")


def t_multipliers_edge():
    if m.make_multipliers(0) != []:
        return "для n = 0 ожидался пустой список"
    fs = m.make_multipliers(3)
    first = [f(2) for f in fs]
    second = [f(7) for f in reversed(fs)]
    bad = expect(first, [0, 2, 4], "[f(2) for f in make_multipliers(3)]")
    if bad:
        return bad
    return expect(second, [14, 7, 0], "вызов функций в обратном порядке")


RECS = [
    {"name": "b", "age": 30},
    {"name": "a", "age": 30},
    {"name": "c", "age": 20},
]


def t_sort_basic():
    got = m.sort_records(RECS, "age")
    return expect([r["name"] for r in got], ["c", "b", "a"], "sort_records(RECS, 'age')")


def t_sort_multi_key():
    got = m.sort_records(RECS, "age", "name")
    bad = expect([r["name"] for r in got], ["c", "a", "b"], "sort_records(RECS, 'age', 'name')")
    if bad:
        return bad
    got = m.sort_records(RECS, "name")
    return expect([r["name"] for r in got], ["a", "b", "c"], "sort_records(RECS, 'name')")


def t_sort_no_mutation():
    data = [dict(r) for r in RECS]
    before = [r["name"] for r in data]
    result = m.sort_records(data, "name")
    if [r["name"] for r in data] != before:
        return "исходный список изменён: используйте sorted, а не list.sort"
    if result is data:
        return "нужно вернуть новый список, а не исходный объект"
    same = m.sort_records(data)
    if same is data or same != data:
        return "без полей нужна копия списка в исходном порядке"


case("compose-basic", t_compose_basic)
case("counter-basic", t_counter_basic)
case("multipliers-basic", t_multipliers_basic)
case("sort-basic", t_sort_basic)
case("compose-identity", t_compose_identity)
case("counter-independent", t_counter_independent)
case("multipliers-edge", t_multipliers_edge)
case("sort-multi-key", t_sort_multi_key)
case("sort-no-mutation", t_sort_no_mutation)
