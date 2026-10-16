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

import cancel_task as m


async def never():
    await asyncio.Event().wait()


def t_timeout_basic():
    async def main():
        a = await m.run_with_timeout(lambda: asyncio.sleep(0, "x"), 5)
        b = await m.run_with_timeout(never, 0.02)
        return a, b

    return expect(run_async(main), (("ok", "x"), ("timeout", None)), "run_with_timeout: успех и таймаут")


def t_cleanup_normal_cancel():
    async def main():
        log = []

        async def work():
            await asyncio.sleep(0)
            return 7

        r = await m.worker_with_cleanup(work, log)
        log2 = []
        started = asyncio.Event()

        async def hang():
            started.set()
            await never()

        t = asyncio.create_task(m.worker_with_cleanup(hang, log2))
        await started.wait()
        t.cancel()
        try:
            await t
        except asyncio.CancelledError:
            pass
        return r, log, log2, t.cancelled()

    return expect(run_async(main), (7, ["acquire", "release"], ["acquire", "release"], True), "worker_with_cleanup: успех и отмена")


def t_cancel_and_wait_basic():
    async def main():
        started = asyncio.Event()

        async def job():
            started.set()
            await never()

        t = asyncio.create_task(job())
        await started.wait()
        res = await m.cancel_and_wait(t)
        return res, t.done(), t.cancelled()

    return expect(run_async(main), (True, True, True), "cancel_and_wait(ожидающая задача) -> (результат, done, cancelled)")


def t_timeout_details():
    async def main():
        log = []

        async def slow():
            try:
                await never()
            finally:
                log.append("cleaned")

        r1 = await m.run_with_timeout(slow, 0.02)

        async def bad():
            raise TypeError("mine")

        try:
            await m.run_with_timeout(bad, 5)
        except TypeError:
            r2 = "TypeError"
        else:
            r2 = "no error"

        t = asyncio.create_task(m.run_with_timeout(never, 30))
        await asyncio.sleep(0)
        t.cancel()
        try:
            await t
        except asyncio.CancelledError:
            r3 = "cancelled"
        else:
            r3 = "returned"
        return r1, log, r2, r3

    return expect(run_async(main), (("timeout", None), ["cleaned"], "TypeError", "cancelled"), "run_with_timeout: очистка, свои ошибки, внешняя отмена")


def t_cleanup_errors():
    async def main():
        log = []

        async def bad():
            raise KeyError("k")

        try:
            await m.worker_with_cleanup(bad, log)
        except KeyError:
            return "KeyError", log
        return "no error", log

    return expect(run_async(main), ("KeyError", ["acquire", "release"]), "worker_with_cleanup при ошибке work")


def t_cancel_edge():
    async def main():
        async def quick():
            return 1

        t = asyncio.create_task(quick())
        await t
        r1 = await m.cancel_and_wait(t)

        started = asyncio.Event()

        async def stubborn():
            started.set()
            try:
                await never()
            except asyncio.CancelledError:
                return "ignored cancel"

        t2 = asyncio.create_task(stubborn())
        await started.wait()
        r2 = await m.cancel_and_wait(t2)
        return r1, t.result(), r2, t2.result()

    return expect(run_async(main), (False, 1, False, "ignored cancel"), "cancel_and_wait для завершённой и упрямой задач")


def t_collect_errors():
    async def main():
        barrier = asyncio.Barrier(4)
        survivors = []

        async def fail(exc):
            await barrier.wait()
            raise exc

        async def hang():
            try:
                await barrier.wait()
                await never()
            except asyncio.CancelledError:
                survivors.append("cancelled")
                raise

        async def ok():
            return 1

        got = await m.collect_errors([lambda: fail(ValueError("a")), lambda: fail(KeyError("b")), lambda: fail(ValueError("c")), hang])
        empty = await m.collect_errors([ok, ok])
        return got, empty, survivors

    return expect(run_async(main, 2.5), ({"KeyError": 1, "ValueError": 2}, {}, ["cancelled"]), "collect_errors")


CASES = [
    ("timeout-basic", t_timeout_basic),
    ("cleanup-normal-cancel", t_cleanup_normal_cancel),
    ("cancel-and-wait-basic", t_cancel_and_wait_basic),
    ("timeout-details", t_timeout_details),
    ("cleanup-errors", t_cleanup_errors),
    ("cancel-edge", t_cancel_edge),
    ("collect-errors", t_collect_errors),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
