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
import functools
import threading
import time

import futures_task as m


def top_level(x):
    return x + 1


class Spy(m.ThreadPoolExecutor):
    created = []
    closed = []

    def __init__(self, *args, **kwargs):
        Spy.created.append((args, dict(kwargs)))
        super().__init__(*args, **kwargs)

    def shutdown(self, *args, **kwargs):
        Spy.closed.append(True)
        return super().shutdown(*args, **kwargs)


def t_collect_basic():
    values, errors = call_with_timeout(lambda: m.run_collect(lambda x: x * 10, [3, 1, 2]))
    return expect((values, errors), ({0: 30, 1: 10, 2: 20}, {}), "run_collect(x*10, [3,1,2])")


def t_collect_errors():
    def f(x):
        return 10 // x

    values, errors = call_with_timeout(lambda: m.run_collect(f, [5, 0, 2, 0]))
    bad = expect(values, {0: 2, 2: 5}, "values")
    if bad:
        return bad
    if sorted(errors) != [1, 3] or not all(isinstance(e, ZeroDivisionError) for e in errors.values()):
        return f"errors должен содержать ZeroDivisionError по индексам 1 и 3, получено {errors!r}"


def t_chunked_basic():
    bad = expect(m.chunked([1, 2, 3, 4, 5], 2), [[1, 2], [3, 4], [5]], "chunked([1..5], 2)")
    if bad:
        return bad
    return expect(m.chunked([], 3), [], "chunked([], 3)")


def t_picklable():
    bad = expect(m.check_picklable(top_level), True, "check_picklable(функция верхнего уровня)")
    if bad:
        return bad
    bad = expect(m.check_picklable(lambda x: x), False, "check_picklable(lambda)")
    if bad:
        return bad
    return expect(m.check_picklable({"a": [1, 2, (3,)]}), True, "check_picklable(dict)")


def t_pool_contract():
    Spy.created.clear()
    Spy.closed.clear()
    real = m.ThreadPoolExecutor
    m.ThreadPoolExecutor = Spy
    barrier = threading.Barrier(3, timeout=2)
    try:
        def f(x):
            barrier.wait()
            return x

        values, errors = call_with_timeout(lambda: m.run_collect(f, range(6), max_workers=3))
    finally:
        m.ThreadPoolExecutor = real
    if len(Spy.created) != 1:
        return f"ожидался один ThreadPoolExecutor, создано {len(Spy.created)}"
    args, kwargs = Spy.created[0]
    workers = kwargs.get("max_workers", args[0] if args else None)
    if workers != 3:
        return f"max_workers должен быть 3, передано {workers!r}"
    if not Spy.closed:
        return "пул не закрыт: используйте with ThreadPoolExecutor(...) as pool"
    return expect((values, errors), ({i: i for i in range(6)}, {}), "результаты при 3 одновременных работниках")


def t_map_strict():
    bad = expect(call_with_timeout(lambda: m.map_strict(top_level, [1, 2, 3])), [2, 3, 4], "map_strict(top_level, [1,2,3])")
    if bad:
        return bad
    release = threading.Event()

    def f(x):
        if x == 2:
            release.wait(2)          # ошибка элемента 2 случается позже по времени...
            raise KeyError("first-by-order")
        if x == 4:
            raise ValueError("later-by-order")   # ...чем ошибка элемента 4
        return x

    def go():
        try:
            m.map_strict(f, [0, 1, 2, 3, 4], max_workers=5)
        except Exception as exc:
            return type(exc).__name__
        return None

    timer = threading.Timer(0.2, release.set)
    timer.start()
    try:
        got = call_with_timeout(go)
    finally:
        release.set()
        timer.cancel()
    return expect(got, "KeyError", "тип исключения map_strict (ошибка элемента с наименьшим индексом, а не первая по времени)")


def t_picklable_edge():
    cases = [
        (functools.partial(top_level, 1), True, "functools.partial(функция верхнего уровня)"),
        (threading.Lock(), False, "threading.Lock()"),
        ((x for x in range(3)), False, "генератор"),
        ((1, "a", None, 2.5), True, "кортеж простых значений"),
    ]

    def nested():
        def inner():
            return 1
        return inner

    cases.append((nested(), False, "вложенная функция"))
    for obj, want, label in cases:
        bad = expect(m.check_picklable(obj), want, f"check_picklable({label})")
        if bad:
            return bad


def t_chunked_edge():
    bad = expect(m.chunked((1, 2, 3), 10), [[1, 2, 3]], "chunked((1,2,3), 10)")
    if bad:
        return bad
    bad = expect(m.chunked("abcd", 2), [["a", "b"], ["c", "d"]], "chunked('abcd', 2)")
    if bad:
        return bad
    for size in (0, -1):
        try:
            m.chunked([1], size)
        except ValueError:
            continue
        return f"chunked([1], {size}) должен поднимать ValueError"


CASES = [
    ("collect-basic", t_collect_basic),
    ("collect-errors", t_collect_errors),
    ("chunked-basic", t_chunked_basic),
    ("picklable", t_picklable),
    ("pool-contract", t_pool_contract),
    ("map-strict", t_map_strict),
    ("picklable-edge", t_picklable_edge),
    ("chunked-edge", t_chunked_edge),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
