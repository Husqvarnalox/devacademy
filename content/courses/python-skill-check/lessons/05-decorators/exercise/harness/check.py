import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import decorators_check as m


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


def make_flaky(fail_times, error=ValueError):
    state = {"calls": 0}

    def flaky(x, y=0):
        """Документация flaky."""
        state["calls"] += 1
        if state["calls"] <= fail_times:
            raise error(f"сбой {state['calls']}")
        return x + y

    return flaky, state


def t_retry_basic():
    flaky, state = make_flaky(2)
    wrapped = m.retry(3)(flaky)
    bad = expect(wrapped(1, y=2), 3, "retry(3)(flaky)(1, y=2)")
    return bad or expect(state["calls"], 3, "число вызовов при двух сбоях и times=3")


def t_retry_exhausted():
    flaky, state = make_flaky(10)
    wrapped = m.retry(3)(flaky)
    try:
        wrapped(1)
    except ValueError as exc:
        if str(exc) != "сбой 3":
            return f"должно пробрасываться последнее исключение ('сбой 3'), получено {str(exc)!r}"
    else:
        return "после трёх неудачных попыток ожидалось исключение"
    return expect(state["calls"], 3, "число вызовов при постоянных сбоях и times=3")


def t_retry_selective():
    flaky, state = make_flaky(5, error=KeyError)
    wrapped = m.retry(4, exceptions=(ValueError,))(flaky)
    try:
        wrapped(1)
    except KeyError:
        return expect(state["calls"], 1, "число вызовов: KeyError вне exceptions не повторяется")
    return "исключение вне exceptions должно пробрасываться"


def t_retry_invalid():
    try:
        m.retry(0)
    except ValueError:
        return None
    return "retry(0) должна вызвать ValueError"


def t_wraps():
    flaky, _ = make_flaky(0)
    for name, wrapper in [("retry", m.retry(2)(flaky)), ("memoize", m.memoize(flaky))]:
        if wrapper.__name__ != "flaky":
            return f"{name}: __name__ равно {wrapper.__name__!r}, ожидалось 'flaky'"
        if wrapper.__doc__ != "Документация flaky.":
            return f"{name}: __doc__ не сохранён"
        if getattr(wrapper, "__wrapped__", None) is not flaky:
            return f"{name}: нет __wrapped__ (используйте functools.wraps)"


def t_memo_basic():
    calls = []

    @m.memoize
    def square(n):
        calls.append(n)
        return n * n

    bad = expect([square(3), square(3), square(4)], [9, 9, 16], "значения square")
    return bad or expect(calls, [3, 4], "реальные вызовы square")


def t_memo_kwargs():
    calls = []

    @m.memoize
    def power(base, exp=2):
        calls.append((base, exp))
        return base ** exp

    values = [power(2), power(2, 3), power(2, exp=3), power(2, 3), power(base=2, exp=3)]
    bad = expect(values, [4, 8, 8, 8, 8], "значения power")
    bad = bad or expect(len(calls) <= 4, True, f"слишком много реальных вызовов: {calls}")
    if bad:
        return bad
    count_before = len(calls)
    power.cache_clear()
    power(2)
    return expect(len(calls), count_before + 1, "вызовов после cache_clear()")


case("retry-basic", t_retry_basic)
case("memo-basic", t_memo_basic)
case("retry-exhausted", t_retry_exhausted)
case("retry-selective", t_retry_selective)
case("retry-invalid", t_retry_invalid)
case("wraps", t_wraps)
case("memo-kwargs", t_memo_kwargs)
