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

import service_task as m


async def spin(n=5):
    for _ in range(n):
        await asyncio.sleep(0)


def leftover_tasks():
    return [t for t in asyncio.all_tasks() if t is not asyncio.current_task() and not t.done()]


def t_stop_signal():
    async def main():
        queue = asyncio.Queue()
        for i in range(12):
            queue.put_nowait(i)
        stop = asyncio.Event()
        started_after_stop = []
        started = []

        async def handler(item):
            if stop.is_set():
                started_after_stop.append(item)
            started.append(item)
            if item == 3:
                stop.set()
            await spin(2)

        processed, remaining = await m.serve(queue, handler, 2, stop)
        await spin()
        return processed, remaining, started_after_stop, started, leftover_tasks()

    processed, remaining, late, started, left = run_async(main)
    if late:
        return f"после stop начались обработки: {late}"
    if sorted(processed + remaining) != list(range(12)) or len(processed) + len(remaining) != 12:
        return f"processed и remaining вместе должны дать исходные 12 элементов без повторов: {processed} + {remaining}"
    if 3 not in processed:
        return "элемент 3, во время которого выставили stop, должен быть обработан до конца"
    if sorted(processed) != sorted(started):
        return "каждый взятый элемент должен быть обработан"
    if not remaining or remaining != sorted(remaining):
        return f"remaining должен быть непустым и в порядке очереди, получено {remaining}"
    if left:
        return f"остались висящие задачи: {len(left)}"


def t_idle_stop():
    async def main():
        queue = asyncio.Queue()
        stop = asyncio.Event()

        async def handler(item):
            pass

        async def stopper():
            await spin(3)
            stop.set()

        s = asyncio.create_task(stopper())
        res = await m.serve(queue, handler, 3, stop)
        await s
        await spin()
        return res, len(leftover_tasks())

    return expect(run_async(main), (([], []), 0), "serve для простаивающей службы -> ((processed, remaining), висящие задачи)")


def t_in_flight():
    async def main():
        queue = asyncio.Queue()
        for i in range(5):
            queue.put_nowait(i)
        stop = asyncio.Event()
        release = {i: asyncio.Event() for i in range(5)}
        started = []

        async def handler(item):
            started.append(item)
            await release[item].wait()

        task = asyncio.create_task(m.serve(queue, handler, 2, stop))
        while len(started) < 2:
            await asyncio.sleep(0)
        stop.set()
        await spin(10)
        done_early = task.done()
        release[0].set()
        release[1].set()
        res = await task
        return done_early, started, res

    done_early, started, (processed, remaining) = run_async(main)
    if done_early:
        return "serve завершилась, не дождавшись обработки взятых элементов"
    return expect((started, sorted(processed), remaining), ([0, 1], [0, 1], [2, 3, 4]), "(начатые, обработанные, остаток)")


def t_stop_before_start():
    async def main():
        queue = asyncio.Queue()
        for i in range(4):
            queue.put_nowait(i)
        stop = asyncio.Event()
        stop.set()
        calls = []

        async def handler(item):
            calls.append(item)

        res = await m.serve(queue, handler, 3, stop)
        return res, calls

    return expect(run_async(main), (([], [0, 1, 2, 3]), []), "serve с заранее выставленным stop -> (результат, вызовы handler)")


def t_late_items():
    async def main():
        queue = asyncio.Queue()
        stop = asyncio.Event()
        handled = []

        async def handler(item):
            await spin(2)
            handled.append(item)

        task = asyncio.create_task(m.serve(queue, handler, 2, stop))
        await spin(3)
        for i in range(5):
            await queue.put(i)
            await asyncio.sleep(0)
        for _ in range(200):
            if len(handled) == 5:
                break
            await asyncio.sleep(0)
        stop.set()
        processed, remaining = await task
        await spin()
        return sorted(processed), remaining, len(leftover_tasks())

    return expect(run_async(main), ([0, 1, 2, 3, 4], [], 0), "serve: элементы, пришедшие во время работы")


CASES = [
    ("serve-stop-signal", t_stop_signal),
    ("serve-idle-stop", t_idle_stop),
    ("serve-in-flight", t_in_flight),
    ("serve-stop-before-start", t_stop_before_start),
    ("serve-late-items", t_late_items),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
