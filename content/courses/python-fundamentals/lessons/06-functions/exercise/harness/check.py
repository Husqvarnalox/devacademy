import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import functions_task as m


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


def t_clamp_basic():
    for args, want in [((5, 0, 10), 5), ((-3, 0, 10), 0), ((42, 0, 10), 10), ((10, 0, 10), 10)]:
        bad = expect(m.clamp(*args), want, f"clamp{args}")
        if bad:
            return bad


def t_clamp_defaults():
    for args, want in [((150,), 100), ((-5,), 0), ((7,), 7), ((50, 60), 60), ((50, 10, 20), 20)]:
        bad = expect(m.clamp(*args), want, f"clamp{args}")
        if bad:
            return bad


def t_add_item_fresh():
    first = m.add_item("a", [])
    bad = expect(first, ["a"], "add_item('a', [])")
    if bad:
        return bad
    return expect(m.add_item(1, [0]), [0, 1], "add_item(1, [0])")


def t_add_item_independent():
    one = m.add_item(1)
    two = m.add_item(2)
    three = m.add_item(3)
    if two != [2] or three != [3] or one is two:
        return f"вызовы без bucket делят один список: после трёх вызовов получили {one!r}, {two!r}, {three!r}"


def t_add_item_given_bucket():
    bucket = [10, 20]
    result = m.add_item(30, bucket)
    if result is not bucket:
        return "при переданном bucket функция должна вернуть тот же самый список, а не копию"
    return expect(bucket, [10, 20, 30], "bucket после add_item(30, bucket)")


def t_min_max_basic():
    for nums, want in [([3, 1, 2], (1, 3)), ([5], (5, 5)), ([-2, -9, 4], (-9, 4))]:
        bad = expect(m.min_max(nums), want, f"min_max({nums})")
        if bad:
            return bad


def t_min_max_empty():
    got = m.min_max([])
    if got is not None:
        return f"min_max([]) вернуло {got!r}, ожидалось None"


def t_greet_basic():
    for call, want in [
        (lambda: m.greet("Анна"), "Привет, Анна!"),
        (lambda: m.greet("Анна", "Здравствуйте"), "Здравствуйте, Анна!"),
        (lambda: m.greet(name="Ян", greeting="Hi"), "Hi, Ян!"),
    ]:
        bad = expect(call(), want, "greet(...)")
        if bad:
            return bad


def t_greet_keyword_only():
    bad = expect(m.greet("Анна", loud=True), "ПРИВЕТ, АННА!", "greet('Анна', loud=True)")
    if bad:
        return bad
    try:
        m.greet("Анна", "Привет", True)
    except TypeError:
        return None
    return "параметр loud должен быть только именованным (после *): позиционная передача должна давать TypeError"


case("clamp-basic", t_clamp_basic)
case("add-item-fresh", t_add_item_fresh)
case("min-max-basic", t_min_max_basic)
case("greet-basic", t_greet_basic)
case("clamp-defaults", t_clamp_defaults)
case("add-item-independent", t_add_item_independent)
case("add-item-given-bucket", t_add_item_given_bucket)
case("min-max-empty", t_min_max_empty)
case("greet-keyword-only", t_greet_keyword_only)
