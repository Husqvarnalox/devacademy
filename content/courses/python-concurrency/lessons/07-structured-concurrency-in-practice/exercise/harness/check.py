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
import concurrent.futures

import bridge as m


def t_basic():
    async def where(x):
        await asyncio.sleep(0)
        return x * 2, threading.current_thread().name

    with m.AsyncBridge() as b:
        r1 = b.run(where, 21)
        r2 = b.run(where, 4)
    bad = expect((r1, r2), ((42, "async-bridge"), (8, "async-bridge")), "run(where, x) -> (результат, имя потока)")
    if bad:
        return bad
    if threading.current_thread().name == "async-bridge":
        return "вызывающий поток не должен быть потоком моста"


def t_error():
    async def boom():
        raise KeyError("nope")

    with m.AsyncBridge() as b:
        try:
            b.run(boom)
        except KeyError:
            pass
        else:
            return "исключение корутины должно дойти до вызывающего"
        return expect(b.run(lambda: asyncio.sleep(0, "still works")), "still works", "run после ошибки")


def t_concurrent():
    state = {}

    async def meet():
        if "event" not in state:
            state["event"] = asyncio.Event()
            state["count"] = 0
        state["count"] += 1
        if state["count"] == 3:
            state["event"].set()
        await state["event"].wait()
        return state["count"]

    results = []
    errors = []
    with m.AsyncBridge() as b:
        def caller():
            try:
                results.append(b.run(meet, timeout=2))
            except BaseException as exc:
                errors.append(exc)

        ts = [threading.Thread(target=caller, daemon=True) for _ in range(3)]
        for t in ts:
            t.start()
        for t in ts:
            t.join(3)
    if errors:
        return f"ошибка в вызывающем потоке: {errors[0]!r} (три вызова должны выполняться конкурентно в одном цикле)"
    return expect(results, [3, 3, 3], "три конкурентных run")


def t_closed():
    b = m.AsyncBridge().start()
    thread_before = [t for t in threading.enumerate() if t.name == "async-bridge"]
    if len(thread_before) != 1:
        return "start() должен запускать ровно один поток async-bridge"
    b.close()
    b.close()
    if thread_before[0].is_alive():
        return "после close поток моста всё ещё жив"

    async def noop():
        return 1

    try:
        b.run(noop)
    except RuntimeError:
        pass
    else:
        return "run после close должен поднимать RuntimeError"
    fresh = m.AsyncBridge()
    try:
        fresh.run(noop)
    except RuntimeError:
        return None
    return "run на незапущенном мосте должен поднимать RuntimeError"


def t_timeout():
    cleaned = threading.Event()

    async def slow():
        try:
            await asyncio.Event().wait()
        finally:
            cleaned.set()

    with m.AsyncBridge() as b:
        try:
            b.run(slow, timeout=0.05)
        except TimeoutError:
            pass
        else:
            return "по истечении timeout должен подниматься TimeoutError"
        if not cleaned.wait(2):
            return "после таймаута корутина должна быть отменена (finally не сработал)"


def t_shutdown_cancels():
    started = threading.Event()
    cleaned = threading.Event()
    box = {}

    async def long_job():
        started.set()
        try:
            await asyncio.Event().wait()
        finally:
            cleaned.set()

    b = m.AsyncBridge().start()

    def caller():
        try:
            b.run(long_job)
        except BaseException as exc:
            box["exc"] = type(exc).__name__

    t = threading.Thread(target=caller, daemon=True)
    t.start()
    if not started.wait(1):
        return "корутина не стартовала"
    b.close()
    t.join(2)
    if not cleaned.is_set():
        return "close должен отменять работающие корутины (их finally обязан выполниться)"
    if t.is_alive():
        return "вызывающий поток остался заблокированным после close"
    return expect(box.get("exc"), "CancelledError", "исключение у вызывающего потока при остановке моста")


def t_reentrant():
    with m.AsyncBridge() as b:
        async def inner():
            return 1

        async def outer():
            try:
                b.run(inner)
            except RuntimeError:
                return "RuntimeError"
            return "no error"

        return expect(b.run(outer, timeout=2), "RuntimeError", "run из потока моста")


CASES = [
    ("bridge-basic", t_basic),
    ("bridge-error", t_error),
    ("bridge-concurrent", t_concurrent),
    ("bridge-closed", t_closed),
    ("bridge-timeout", t_timeout),
    ("bridge-shutdown-cancels", t_shutdown_cancels),
    ("bridge-reentrant", t_reentrant),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
