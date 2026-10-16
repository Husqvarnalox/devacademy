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


import itertools

import lazy_task as m


def t_chunked_basic():
    bad = expect(list(m.chunked("abcde", 2)), [["a", "b"], ["c", "d"], ["e"]], "list(chunked('abcde', 2))")
    if bad:
        return bad
    bad = expect(list(m.chunked(range(6), 3)), [[0, 1, 2], [3, 4, 5]], "list(chunked(range(6), 3))")
    if bad:
        return bad
    return expect(list(m.chunked([], 4)), [], "list(chunked([], 4))")


def t_chunked_lazy():
    first = next(m.chunked(itertools.count(), 3))
    bad = expect(first, [0, 1, 2], "next(chunked(count(), 3))")
    if bad:
        return bad
    pulled = []

    def source():
        for i in range(100):
            pulled.append(i)
            yield i

    g = m.chunked(source(), 4)
    next(g)
    if len(pulled) > 4:
        return f"после первого фрагмента из источника прочитано {len(pulled)} элементов, ожидалось не больше 4"


def t_chunked_bad_size():
    for size in (0, -1):
        try:
            list(m.chunked([1, 2, 3], size))
        except ValueError:
            continue
        return f"chunked(..., {size}) должен возбуждать ValueError при итерации"


def t_countdown_basic():
    bad = expect(list(m.Countdown(3)), [3, 2, 1], "list(Countdown(3))")
    if bad:
        return bad
    c = m.Countdown(2)
    got = [next(c), next(c)]
    bad = expect(got, [2, 1], "два вызова next(Countdown(2))")
    if bad:
        return bad
    try:
        next(c)
    except StopIteration:
        return None
    return "после последнего элемента next() должен поднимать StopIteration"


def t_countdown_exhausted():
    c = m.Countdown(3)
    if iter(c) is not c:
        return "iter(c) должен возвращать сам объект c"
    bad = expect(list(c), [3, 2, 1], "первый проход")
    if bad:
        return bad
    bad = expect(list(c), [], "повторный проход по исчерпанному итератору")
    if bad:
        return bad
    return expect(list(m.Countdown(0)) + list(m.Countdown(-2)), [], "Countdown(0) и Countdown(-2)")


def t_flatten_basic():
    got = list(m.flatten([1, [2, (3, 4)], 5]))
    bad = expect(got, [1, 2, 3, 4, 5], "flatten([1, [2, (3, 4)], 5])")
    if bad:
        return bad
    return expect(list(m.flatten([])), [], "flatten([])")


def t_flatten_deep_strings():
    deep = [1]
    for _ in range(50):
        deep = [deep, "ab"]
    got = list(m.flatten(deep))
    expected = [1] + ["ab"] * 50
    bad = expect(got, expected, "flatten с глубиной 50 и строками")
    if bad:
        return bad
    g = m.flatten([["x"], "yz"])
    if not hasattr(g, "__next__"):
        return "flatten должен быть генератором (ленивым итератором)"
    return expect(list(g), ["x", "yz"], "flatten([['x'], 'yz'])")


def t_runs_basic():
    return expect(m.run_lengths("aabccc"), [("a", 2), ("b", 1), ("c", 3)], "run_lengths('aabccc')")


def t_runs_nonconsecutive():
    bad = expect(m.run_lengths([1, 1, 2, 1]), [(1, 2), (2, 1), (1, 1)], "run_lengths([1, 1, 2, 1])")
    if bad:
        return bad
    return expect(m.run_lengths([]), [], "run_lengths([])")


case("chunked-basic", t_chunked_basic)
case("countdown-basic", t_countdown_basic)
case("flatten-basic", t_flatten_basic)
case("runs-basic", t_runs_basic)
case("chunked-lazy", t_chunked_lazy)
case("chunked-bad-size", t_chunked_bad_size)
case("countdown-exhausted", t_countdown_exhausted)
case("flatten-deep-strings", t_flatten_deep_strings)
case("runs-nonconsecutive", t_runs_nonconsecutive)
