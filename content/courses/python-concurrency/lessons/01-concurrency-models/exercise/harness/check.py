import asyncio
import json
import os
import threading

NONCE = os.environ["DEVACADEMY_NONCE"]


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def call_with_timeout(fn, seconds=2.0):
    """Run fn() in a daemon thread; a hang becomes a TimeoutError instead of freezing the harness."""
    box = {}

    def target():
        try:
            box["value"] = fn()
        except BaseException as exc:  # the learner's code may raise anything
            box["error"] = exc

    t = threading.Thread(target=target, daemon=True)
    t.start()
    t.join(seconds)
    if t.is_alive():
        raise TimeoutError(f"код не завершился за {seconds} с (зависание или взаимная блокировка)")
    if "error" in box:
        raise box["error"]
    return box.get("value")


def run_async(coro_func, seconds=1.5):
    """Run coro_func() in a fresh event loop with an overall deadline."""
    async def main():
        async with asyncio.timeout(seconds):
            return await coro_func()

    return asyncio.run(main())


def case(case_id, check):
    """check() returns None on success or a string that explains the failure."""
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

import scheduler as m


def gen(values, ret=None, fail=None):
    for v in values:
        yield v
    if fail is not None:
        raise fail
    return ret


def t_round_robin():
    tasks = {"a": gen([1, 2], "A"), "b": gen([10], "B")}
    trace, results = m.run(tasks)
    return expect(trace, [("a", 1), ("b", 10), ("a", 2)], "trace для a=[1,2], b=[10]")


def t_results_order():
    tasks = {"a": gen([1, 2], "A"), "b": gen([10], "B")}
    _, results = m.run(tasks)
    bad = expect(results, {"a": "A", "b": "B"}, "results")
    if bad:
        return bad
    return expect(list(results), ["b", "a"], "порядок ключей results (порядок завершения)")


def t_recommend_basic():
    for args, want in [((True, False), "processes"), ((False, True), "asyncio"), ((False, False), "threads")]:
        bad = expect(m.recommend(*args), want, f"recommend{args}")
        if bad:
            return bad


def t_error_isolated():
    boom = ValueError("boom")
    tasks = {"x": gen([1], fail=boom), "y": gen([2, 3], "Y")}
    trace, results = m.run(tasks)
    bad = expect(trace, [("x", 1), ("y", 2), ("y", 3)], "trace")
    if bad:
        return bad
    if results.get("x") is not boom:
        return f"results['x'] должен быть объектом исключения, а не {results.get('x')!r}"
    return expect(results.get("y"), "Y", "results['y']")


def t_empty_and_immediate():
    bad = expect(m.run({}), ([], {}), "run({})")
    if bad:
        return bad
    trace, results = m.run({"q": gen([], "done")})
    return expect((trace, results), ([], {"q": "done"}), "run с мгновенно завершающейся задачей")


def t_uneven():
    tasks = {"a": gen([1], "A"), "b": gen([1, 2, 3], "B"), "c": gen([1, 2], "C")}
    trace, results = m.run(tasks)
    bad = expect(trace, [("a", 1), ("b", 1), ("c", 1), ("b", 2), ("c", 2), ("b", 3)], "trace")
    if bad:
        return bad
    return expect(list(results), ["a", "c", "b"], "порядок завершения")


def t_recommend_priority():
    return expect(m.recommend(True, True), "processes", "recommend(True, True)")


CASES = [
    ("round-robin", t_round_robin),
    ("results-order", t_results_order),
    ("recommend-basic", t_recommend_basic),
    ("error-isolated", t_error_isolated),
    ("empty-and-immediate", t_empty_and_immediate),
    ("uneven-lengths", t_uneven),
    ("recommend-priority", t_recommend_priority),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
