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

import async_task as m


def t_gather_concurrent():
    async def main():
        ev_a, ev_b = asyncio.Event(), asyncio.Event()

        async def a():
            ev_a.set()
            await ev_b.wait()
            return "a"

        async def b():
            ev_b.set()
            await ev_a.wait()
            return "b"

        return await m.gather_ok([a(), b()])

    try:
        got = run_async(main, 1.0)
    except TimeoutError:
        return "gather_ok выполняет корутины последовательно: две взаимозависимые корутины не завершились"
    return expect(got, ["a", "b"], "gather_ok([a(), b()])")


def t_reverse_finish():
    return expect(run_async(lambda: m.reverse_finish(3)), ["start 0", "start 1", "start 2", "end 2", "end 1", "end 0"], "reverse_finish(3)")


def t_blocking_offload():
    async def main():
        ticks = 0
        enough = threading.Event()

        def blocker(x):
            ok = enough.wait(1.5)
            return x * 2, ok

        async def ticker():
            nonlocal ticks
            while ticks < 3:
                ticks += 1
                await asyncio.sleep(0)
            enough.set()

        result, _ = await asyncio.gather(m.run_blocking(blocker, 21), ticker())
        return result

    got = run_async(main, 2.5)
    return expect(got, (42, True), "run_blocking(blocker, 21): цикл должен продолжать работать, пока функция блокируется")


def t_gather_errors():
    async def main():
        log = []

        async def boom():
            await asyncio.sleep(0)
            raise ValueError("boom")

        async def hang():
            try:
                await asyncio.Event().wait()
            except asyncio.CancelledError:
                log.append("cancelled")
                raise

        seen = []
        try:
            await m.gather_ok([hang(), boom()])
        except* ValueError as eg:
            seen.append(len(eg.exceptions))
        return ("group" if seen else "no group", sum(seen), log)

    got = run_async(main, 2.0)
    return expect(got, ("group", 1, ["cancelled"]), "поведение gather_ok при ошибке (ExceptionGroup + отмена соседа)")


def t_reverse_sizes():
    for n, want in [
        (0, []),
        (1, ["start 0", "end 0"]),
        (6, [f"start {i}" for i in range(6)] + [f"end {i}" for i in reversed(range(6))]),
    ]:
        bad = expect(run_async(lambda: m.reverse_finish(n)), want, f"reverse_finish({n})")
        if bad:
            return bad


def t_blocking_args():
    def f(a, b=0):
        return a - b

    def g():
        raise KeyError("k")

    async def main():
        r = await m.run_blocking(f, 10, b=3)
        try:
            await m.run_blocking(g)
        except KeyError:
            return r, "KeyError"
        return r, None

    return expect(run_async(main), (7, "KeyError"), "run_blocking с аргументами и исключением")


def t_gather_order():
    async def main():
        gate = asyncio.Event()

        async def slow():
            await gate.wait()
            return "slow"

        async def fast():
            gate.set()
            return "fast"

        return await m.gather_ok([slow(), fast()])

    return expect(run_async(main, 2.0), ["slow", "fast"], "порядок результатов gather_ok")


CASES = [
    ("gather-concurrent", t_gather_concurrent),
    ("reverse-finish", t_reverse_finish),
    ("blocking-offload", t_blocking_offload),
    ("gather-errors", t_gather_errors),
    ("reverse-sizes", t_reverse_sizes),
    ("blocking-args", t_blocking_args),
    ("gather-order", t_gather_order),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
