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

import merge_task as m


def t_basic():
    got = list(m.merge_sorted([1, 4, 7], [2, 4, 9], [0, 10]))
    return expect(got, [0, 1, 2, 4, 4, 7, 9, 10], "merge_sorted трёх списков")


def t_infinite():
    got = list(itertools.islice(m.merge_sorted(itertools.count(0, 2), itertools.count(1, 2)), 6))
    return expect(got, [0, 1, 2, 3, 4, 5], "первые 6 элементов слияния двух бесконечных потоков")


def t_empty():
    bad = expect(list(m.merge_sorted()), [], "merge_sorted() без аргументов")
    if bad:
        return bad
    bad = expect(list(m.merge_sorted([], [])), [], "merge_sorted([], [])")
    if bad:
        return bad
    return expect(list(m.merge_sorted([], [1, 2], iter([]))), [1, 2], "пустые потоки среди непустых")


def t_lazy():
    pulled = [0, 0]

    def stream(idx, values):
        for v in values:
            pulled[idx] += 1
            yield v

    g = m.merge_sorted(stream(0, [1, 3, 5, 7]), stream(1, [2, 4, 6, 8]))
    first = [next(g), next(g)]
    bad = expect(first, [1, 2], "первые два элемента")
    if bad:
        return bad
    # выдано по одному из каждого потока; допускается лишь по одной голове сверху
    if pulled[0] > 2 or pulled[1] > 2:
        return f"прочитано вперёд слишком много: {pulled} (допустимо не больше 2 из каждого потока)"


def t_stable():
    a = [(1, "a"), (2, "a")]
    b = [(1, "b"), (2, "b")]

    class Key:
        def __init__(self, n, tag):
            self.n, self.tag = n, tag

        def __lt__(self, other):
            return self.n < other.n

        def __eq__(self, other):
            return self.n == other.n

    sa = [Key(n, t) for n, t in a]
    sb = [Key(n, t) for n, t in b]
    got = [(k.n, k.tag) for k in m.merge_sorted(sa, sb)]
    return expect(got, [(1, "a"), (1, "b"), (2, "a"), (2, "b")], "порядок равных элементов")


def t_many():
    streams = [list(range(i, 50, 7)) for i in range(7)]
    got = list(m.merge_sorted(*streams))
    return expect(got, sorted(sum(streams, [])), "слияние семи потоков")


case("merge-basic", t_basic)
case("merge-infinite", t_infinite)
case("merge-empty", t_empty)
case("merge-lazy", t_lazy)
case("merge-stable", t_stable)
case("merge-many", t_many)
