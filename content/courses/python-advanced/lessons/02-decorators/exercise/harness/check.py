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


import decorators_task as m


def meta_ok(wrapped, original):
    return (
        wrapped.__name__ == original.__name__
        and wrapped.__doc__ == original.__doc__
        and getattr(wrapped, "__wrapped__", None) is original
    )


def t_counter_basic():
    def add(a, b=0):
        """Сложение."""
        return a + b

    f = m.call_counter(add)
    if getattr(f, "calls", None) != 0:
        return f"до вызовов f.calls должно быть 0, получено {getattr(f, 'calls', 'нет атрибута')!r}"
    r1 = f(1, b=2)
    r2 = f(5)
    bad = expect((r1, r2), (3, 5), "результаты обёртки")
    if bad:
        return bad
    return expect(f.calls, 2, "f.calls после двух вызовов")


def t_retry_basic():
    state = {"n": 0}

    def flaky():
        state["n"] += 1
        if state["n"] < 3:
            raise ValueError("ещё нет")
        return "ok"

    got = m.retry(3)(flaky)()
    bad = expect(got, "ok", "результат retry(3)(flaky)()")
    if bad:
        return bad
    return expect(state["n"], 3, "число вызовов")


def t_retry_exhausted():
    state = {"n": 0}

    def always():
        state["n"] += 1
        raise ValueError(f"попытка {state['n']}")

    try:
        m.retry(4)(always)()
    except ValueError as exc:
        bad = expect(str(exc), "попытка 4", "сообщение исключения (должно быть последнее)")
        if bad:
            return bad
    else:
        return "после исчерпания попыток исключение должно быть возбуждено повторно"
    return expect(state["n"], 4, "число вызовов функции")


def t_retry_other_exception():
    state = {"n": 0}

    def boom():
        state["n"] += 1
        raise KeyError("x")

    try:
        m.retry(5, exceptions=(ValueError,))(boom)()
    except KeyError:
        return expect(state["n"], 1, "число вызовов при постороннем исключении")
    return "KeyError не входит в exceptions и должен пролететь наружу"


def t_memoize_basic():
    calls = []

    def square(x):
        calls.append(x)
        return x * x

    f = m.memoize(square)
    got = [f(3), f(3), f(4), f(3)]
    bad = expect(got, [9, 9, 16, 9], "результаты memoize")
    if bad:
        return bad
    bad = expect(calls, [3, 4], "реальные вызовы func")
    if bad:
        return bad
    if not isinstance(getattr(f, "cache", None), dict) or (3,) not in f.cache:
        return "у обёртки должен быть dict cache с ключами-кортежами аргументов, например (3,)"


def t_memoize_unhashable():
    f = m.memoize(lambda xs: len(xs))
    try:
        f([1, 2])
    except TypeError:
        return None
    return "для нехешируемого аргумента (list) ожидался TypeError"


def t_tag_basic():
    @m.tag("b")
    def hello(name, punct="!"):
        return f"hi {name}{punct}"

    return expect(hello("Ann", punct="?"), "<b>hi Ann?</b>", "tag('b')(hello)('Ann', punct='?')")


def t_tag_stacking():
    @m.tag("b")
    @m.tag("i")
    def text():
        return "x"

    return expect(text(), "<b><i>x</i></b>", "два стекированных тега")


def t_wraps_metadata():
    def sample(x):
        """Документация sample."""
        return x

    made = {
        "call_counter": m.call_counter(sample),
        "retry": m.retry(2)(sample),
        "memoize": m.memoize(sample),
        "tag": m.tag("p")(sample),
    }
    for name, wrapped in made.items():
        if not meta_ok(wrapped, sample):
            return f"{name}: не сохранены __name__/__doc__/__wrapped__ (нужен functools.wraps)"


case("counter-basic", t_counter_basic)
case("retry-basic", t_retry_basic)
case("memoize-basic", t_memoize_basic)
case("tag-basic", t_tag_basic)
case("wraps-metadata", t_wraps_metadata)
case("retry-exhausted", t_retry_exhausted)
case("retry-other-exception", t_retry_other_exception)
case("memoize-unhashable", t_memoize_unhashable)
case("tag-stacking", t_tag_stacking)
