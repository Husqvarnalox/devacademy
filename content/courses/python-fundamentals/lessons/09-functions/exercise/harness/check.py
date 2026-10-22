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


def close(actual, expected, call):
    if not isinstance(actual, (int, float)) or isinstance(actual, bool) or abs(actual - expected) > 1e-9:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


def t_square():
    for n, want in [(3, 9), (-4, 16), (0, 0)]:
        bad = expect(m.square(n), want, f"square({n})")
        if bad:
            return bad


def t_discount_basic():
    bad = close(m.discount(200, 10), 180, "discount(200, 10)")
    return bad or close(m.discount(50, 50), 25, "discount(50, 50)")


def t_discount_default():
    bad = close(m.discount(200), 180, "discount(200)")
    return bad or close(m.discount(80), 72, "discount(80)")


def t_discount_edges():
    bad = close(m.discount(100, 0), 100, "discount(100, 0)")
    return bad or close(m.discount(100, 100), 0, "discount(100, 100)")


def t_grade_basic():
    for points, want in [(95, "отлично"), (72, "хорошо"), (55, "удовлетворительно"), (10, "неудовлетворительно")]:
        bad = expect(m.grade(points), want, f"grade({points})")
        if bad:
            return bad


def t_grade_borders():
    for points, want in [(85, "отлично"), (84, "хорошо"), (70, "хорошо"), (69, "удовлетворительно"),
                         (50, "удовлетворительно"), (49, "неудовлетворительно"), (0, "неудовлетворительно"),
                         (100, "отлично")]:
        bad = expect(m.grade(points), want, f"grade({points})")
        if bad:
            return bad


def t_sum_to_basic():
    for n, want in [(1, 1), (4, 10), (10, 55)]:
        bad = expect(m.sum_to(n), want, f"sum_to({n})")
        if bad:
            return bad


def t_sum_to_edges():
    for n, want in [(0, 0), (-5, 0), (100, 5050)]:
        bad = expect(m.sum_to(n), want, f"sum_to({n})")
        if bad:
            return bad


def t_returns_not_prints():
    import contextlib
    import io

    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer):
        value = m.square(5)
    if buffer.getvalue():
        return "функции не должны ничего печатать: результат нужно вернуть через return"
    return expect(value, 25, "square(5)")


case("square", t_square)
case("discount-basic", t_discount_basic)
case("grade-basic", t_grade_basic)
case("sum-to-basic", t_sum_to_basic)
case("discount-default", t_discount_default)
case("discount-edges", t_discount_edges)
case("grade-borders", t_grade_borders)
case("sum-to-edges", t_sum_to_edges)
case("no-print", t_returns_not_prints)
