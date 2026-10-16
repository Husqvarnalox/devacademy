import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import rotate_task as m


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


def t_basic():
    for items, k, want in [([1, 2, 3, 4, 5], 2, [4, 5, 1, 2, 3]), ([1, 2, 3], 1, [3, 1, 2]), (list("abcd"), 3, list("bcda"))]:
        data = list(items)
        result = m.rotate_in_place(data, k)
        if result is not None:
            return "функция должна изменять список на месте и возвращать None"
        bad = expect(data, want, f"список после rotate_in_place({items}, {k})")
        if bad:
            return bad


def t_trivial():
    empty = []
    m.rotate_in_place(empty, 3)
    if empty != []:
        return "пустой список должен остаться пустым"
    one = [7]
    m.rotate_in_place(one, 10)
    if one != [7]:
        return f"список из одного элемента не должен меняться, получили {one!r}"
    zero = [1, 2, 3]
    m.rotate_in_place(zero, 0)
    return expect(zero, [1, 2, 3], "список после rotate_in_place([1, 2, 3], 0)")


def t_alias():
    items = [1, 2, 3, 4]
    alias = items
    m.rotate_in_place(items, 1)
    if alias is not items or alias != [4, 1, 2, 3]:
        return f"изменение должно быть видно через другое имя того же списка: {alias!r}"


def t_k_range():
    for k, want in [(7, [4, 5, 1, 2, 3]), (5, [1, 2, 3, 4, 5]), (-1, [2, 3, 4, 5, 1]), (-7, [3, 4, 5, 1, 2]), (10**9 + 7, [4, 5, 1, 2, 3])]:
        data = [1, 2, 3, 4, 5]
        m.rotate_in_place(data, k)
        bad = expect(data, want, f"список после rotate_in_place([1, 2, 3, 4, 5], {k})")
        if bad:
            return bad


def t_large():
    n = 300_000
    data = list(range(n))
    m.rotate_in_place(data, 123_456)
    if data[0] != n - 123_456 or data[-1] != n - 123_457 or len(data) != n:
        return "неверный результат на большом списке"


case("rotate-basic", t_basic)
case("rotate-trivial", t_trivial)
case("rotate-alias", t_alias)
case("rotate-any-k", t_k_range)
case("rotate-large", t_large)
