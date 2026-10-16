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

import crawler as m


class Meter:
    """Считает одновременные вызовы; yields раз за разом, чтобы другие задачи успели войти."""

    def __init__(self, yields=3):
        self.now = 0
        self.peak = 0
        self.calls = 0
        self.yields = yields

    async def __call__(self, x):
        self.now += 1
        self.calls += 1
        self.peak = max(self.peak, self.now)
        try:
            for _ in range(self.yields):
                await asyncio.sleep(0)
            if isinstance(x, str) and x.startswith("bad"):
                raise RuntimeError(x)
            return f"<{x}>"
        finally:
            self.now -= 1


def t_counter_fixed():
    async def main():
        c = m.AsyncCounter()
        got = await asyncio.gather(*(c.add(1) for _ in range(10)))
        return c.value, sorted(got)

    return expect(run_async(main), (10, list(range(1, 11))), "AsyncCounter: 10 конкурентных add(1) -> (value, возвращённые значения)")


def t_crawl_basic():
    async def main():
        order = []

        async def fetch(url):
            for _ in range(3 if url == "a" else 1):
                await asyncio.sleep(0)
            order.append(url)
            return url.upper()

        res = await m.crawl(["a", "b", "c"], fetch, 3)
        return list(res.items()), order

    got, order = run_async(main)
    bad = expect(got, [("a", "A"), ("b", "B"), ("c", "C")], "crawl(['a','b','c'], fetch, 3)")
    if bad:
        return bad
    if order[-1] != "a":
        return "fetch должны выполняться конкурентно: самый медленный адрес завершается последним"


def t_workers_basic():
    async def main():
        async def handler(x):
            for _ in range(5 - x):
                await asyncio.sleep(0)
            return x * x

        return await m.run_workers([1, 2, 3, 4], handler, 2, 2)

    return expect(run_async(main), [1, 4, 9, 16], "run_workers([1,2,3,4], квадрат, 2, 2)")


def t_crawl_limit():
    async def main():
        out = []
        for limit, n in [(3, 10), (1, 4), (5, 3)]:
            meter = Meter()
            res = await m.crawl([f"u{i}" for i in range(n)], meter, limit)
            out.append((limit, meter.peak, meter.calls, len(res)))
        return out

    return expect(run_async(main), [(3, 3, 10, 10), (1, 1, 4, 4), (5, 3, 3, 3)], "crawl: (лимит, пик одновременных fetch, число вызовов, размер результата)")


def t_crawl_errors():
    async def main():
        res = await m.crawl(["ok", "bad1", "fine", "bad2"], Meter(), 2)
        kinds = {k: (type(v).__name__ if isinstance(v, Exception) else v) for k, v in res.items()}
        try:
            await m.crawl(["x"], Meter(), 0)
        except ValueError:
            zero = "ValueError"
        else:
            zero = "no error"
        return kinds, list(res), zero

    return expect(
        run_async(main),
        ({"ok": "<ok>", "bad1": "RuntimeError", "fine": "<fine>", "bad2": "RuntimeError"}, ["ok", "bad1", "fine", "bad2"], "ValueError"),
        "crawl: ошибки как значения, порядок ключей, max_concurrency=0",
    )


def t_workers_bounded():
    created, peak = [], []
    real = asyncio.Queue

    class SpyQueue(real):
        def __init__(self, maxsize=0):
            super().__init__(maxsize)
            created.append(maxsize)

        async def put(self, item):
            await super().put(item)
            peak.append(self.qsize())

    async def main():
        return await m.run_workers(list(range(20)), Meter(), 2, 3)

    asyncio.Queue = SpyQueue
    try:
        res = run_async(main)
    finally:
        asyncio.Queue = real
    if created != [3]:
        return f"ожидалась одна очередь asyncio.Queue(maxsize=3), создано: {created}"
    if peak and max(peak) > 3:
        return f"очередь выросла до {max(peak)} при queue_size=3"
    return expect(res, [f"<{i}>" for i in range(20)], "результаты run_workers")


def t_workers_concurrency():
    async def main():
        meter = Meter()
        res = await m.run_workers(["a", "bad-1", "c", "bad-2", "e", "f", "g", "h"], meter, 3, 2)
        await asyncio.sleep(0)
        left = [t for t in asyncio.all_tasks() if t is not asyncio.current_task() and not t.done()]
        kinds = [type(r).__name__ if isinstance(r, Exception) else r for r in res]
        return meter.peak, meter.calls, kinds, len(left)

    return expect(
        run_async(main),
        (3, 8, ["<a>", "RuntimeError", "<c>", "RuntimeError", "<e>", "<f>", "<g>", "<h>"], 0),
        "run_workers: (пик обработчиков, вызовов, результаты, оставшиеся задачи)",
    )


CASES = [
    ("counter-fixed", t_counter_fixed),
    ("crawl-basic", t_crawl_basic),
    ("workers-basic", t_workers_basic),
    ("crawl-limit", t_crawl_limit),
    ("crawl-errors", t_crawl_errors),
    ("workers-bounded", t_workers_bounded),
    ("workers-concurrency", t_workers_concurrency),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
