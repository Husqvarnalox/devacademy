import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def expect(actual, expected, call):
    if actual != expected:
        return f"{call} вернуло {actual!r}, ожидалось {expected!r}"
    return None


import breaker as m


class Dep:
    """Зависимость, которая умеет падать; считает реальные вызовы."""

    def __init__(self):
        self.calls = 0
        self.fail = False

    def __call__(self, x=0):
        self.calls += 1
        if self.fail:
            raise ConnectionError("down")
        return x + 1


def attempt(cb, dep):
    try:
        return cb.call(dep, 1)
    except ConnectionError:
        return "fail"
    except m.CircuitOpenError:
        return "open"


def t_open_after_threshold():
    t = [0.0]
    dep = Dep()
    cb = m.CircuitBreaker(failure_threshold=3, reset_timeout=10, clock=lambda: t[0])
    if cb.state != "closed" or attempt(cb, dep) != 2:
        return "новый предохранитель замкнут и пропускает вызовы"
    dep.fail = True
    got = [attempt(cb, dep) for _ in range(3)]
    bad = expect((got, cb.state), (["fail"] * 3, "open"), "3 сбоя подряд -> (результаты, state)")
    if bad:
        return bad


def t_open_rejects():
    t = [0.0]
    dep = Dep()
    dep.fail = True
    cb = m.CircuitBreaker(failure_threshold=2, reset_timeout=10, clock=lambda: t[0])
    attempt(cb, dep)
    attempt(cb, dep)
    calls = dep.calls
    t[0] = 9.9
    got = [attempt(cb, dep) for _ in range(3)]
    bad = expect((got, dep.calls - calls), (["open"] * 3, 0), "в состоянии open fn не вызывается")
    if bad:
        return bad


def t_half_open_success():
    t = [0.0]
    dep = Dep()
    dep.fail = True
    cb = m.CircuitBreaker(failure_threshold=2, reset_timeout=10, clock=lambda: t[0])
    attempt(cb, dep)
    attempt(cb, dep)
    t[0] = 10.0
    bad = expect(cb.state, "half_open", "state через reset_timeout")
    if bad:
        return bad
    dep.fail = False
    return expect((attempt(cb, dep), cb.state, attempt(cb, dep)), (2, "closed", 2), "успешный пробный вызов замыкает")


def t_half_open_failure():
    t = [0.0]
    dep = Dep()
    dep.fail = True
    cb = m.CircuitBreaker(failure_threshold=2, reset_timeout=10, clock=lambda: t[0])
    attempt(cb, dep)
    attempt(cb, dep)
    t[0] = 25.0
    bad = expect((attempt(cb, dep), cb.state), ("fail", "open"), "сбой пробного вызова размыкает снова")
    if bad:
        return bad
    t[0] = 34.9
    if cb.state != "open":
        return "таймер должен отсчитываться от последнего размыкания (25.0), а не от первого"
    t[0] = 35.0
    return expect(cb.state, "half_open", "state через 10 с после повторного размыкания")


def t_success_resets():
    t = [0.0]
    dep = Dep()
    cb = m.CircuitBreaker(failure_threshold=3, reset_timeout=10, clock=lambda: t[0])
    for pattern in [True, True, False, True, True, False]:
        dep.fail = pattern
        attempt(cb, dep)
    return expect(cb.state, "closed", "сбои не подряд не размыкают предохранитель")


def t_failure_types():
    t = [0.0]
    cb = m.CircuitBreaker(failure_threshold=1, reset_timeout=10, clock=lambda: t[0], failure_types=(ConnectionError,))

    def boom():
        raise KeyError("bug")

    try:
        cb.call(boom)
    except KeyError:
        pass
    else:
        return "исключение должно пробрасываться"
    bad = expect(cb.state, "closed", "KeyError не входит в failure_types")
    if bad:
        return bad
    for args in [(0, 1), (1, 0)]:
        try:
            m.CircuitBreaker(failure_threshold=args[0], reset_timeout=args[1])
        except ValueError:
            continue
        return f"CircuitBreaker{args} должен поднять ValueError"


case("open-after-threshold", t_open_after_threshold)
case("open-rejects", t_open_rejects)
case("half-open-success", t_half_open_success)
case("half-open-failure", t_half_open_failure)
case("success-resets", t_success_resets)
case("failure-types", t_failure_types)
