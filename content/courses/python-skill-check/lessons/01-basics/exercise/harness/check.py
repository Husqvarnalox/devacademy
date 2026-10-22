import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import basics_check as m


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


def t_collatz():
    for n, want in [(1, 0), (2, 1), (6, 8), (27, 111)]:
        bad = expect(m.collatz_steps(n), want, f"collatz_steps({n})")
        if bad:
            return bad


def t_digit():
    for n, want in [(1234, 10), (7, 7), (0, 0), (1000000, 1), (99999, 45)]:
        bad = expect(m.digit_sum(n), want, f"digit_sum({n})")
        if bad:
            return bad


def t_leap():
    for year, want in [(2024, True), (2023, False), (1900, False), (2000, True), (2100, False), (1996, True)]:
        got = m.is_leap(year)
        if got is not want:
            return f"is_leap({year}) вернуло {got!r}, ожидалось {want!r}"


case("collatz", t_collatz)
case("digit", t_digit)
case("leap", t_leap)
