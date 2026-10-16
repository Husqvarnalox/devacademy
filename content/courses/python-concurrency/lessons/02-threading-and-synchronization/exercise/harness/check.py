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
import queue
import sys
import time

import sync_task as m


class SpyLock:
    def __init__(self):
        self._lock = threading.Lock()
        self.acquired = 0
        self.held = False

    def __enter__(self):
        self._lock.acquire()
        self.acquired += 1
        self.held = True
        return self

    def __exit__(self, *exc):
        self.held = False
        self._lock.release()

    def acquire(self, *a, **k):
        ok = self._lock.acquire(*a, **k)
        if ok:
            self.acquired += 1
            self.held = True
        return ok

    def release(self):
        self.held = False
        self._lock.release()


def t_counter_basic():
    c = m.SafeCounter()
    bad = expect(c.value, 0, "SafeCounter().value")
    if bad:
        return bad
    bad = expect(c.increment(), 1, "increment()")
    if bad:
        return bad
    bad = expect(c.increment(5), 6, "increment(5)")
    if bad:
        return bad
    c2 = m.SafeCounter()
    c2.increment(2)
    got = [c2.increment_if_below(3), c2.increment_if_below(3), c2.value]
    return expect(got, [True, False, 3], "increment_if_below(3) дважды и value")


def t_counter_uses_lock():
    spy = SpyLock()
    c = m.SafeCounter(lock_factory=lambda: spy)
    c.increment()
    c.increment(2)
    _ = c.value
    c.increment_if_below(10)
    c.increment_if_below(0)
    if spy.acquired != 5:
        return f"ожидалось 5 захватов блокировки (по одному на операцию), было {spy.acquired}"
    if spy.held:
        return "блокировка осталась захваченной после операций"


def t_counter_threads():
    c = m.SafeCounter()
    old = sys.getswitchinterval()
    sys.setswitchinterval(1e-6)
    try:
        def work():
            for _ in range(1000):
                c.increment()
        ts = [threading.Thread(target=work) for _ in range(8)]
        for t in ts:
            t.start()
        for t in ts:
            t.join()
    finally:
        sys.setswitchinterval(old)
    bad = expect(c.value, 8000, "value после 8 потоков по 1000 increment")
    if bad:
        return bad
    limited = m.SafeCounter()
    wins = []
    def attempt():
        for _ in range(200):
            if limited.increment_if_below(500):
                wins.append(1)
    ts = [threading.Thread(target=attempt) for _ in range(8)]
    for t in ts:
        t.start()
    for t in ts:
        t.join()
    return expect((limited.value, len(wins)), (500, 500), "(value, число успехов) для increment_if_below(500)")


def t_pipeline_basic():
    bad = expect(call_with_timeout(lambda: m.bounded_pipeline([1, 2, 3, 4], lambda x: x * x)), [1, 4, 9, 16], "bounded_pipeline([1,2,3,4], квадрат)")
    if bad:
        return bad
    def slow_first(x):
        if x == 0:
            time.sleep(0.05)
        return x + 1
    bad = expect(call_with_timeout(lambda: m.bounded_pipeline(range(6), slow_first, workers=3, capacity=1)), [1, 2, 3, 4, 5, 6], "порядок при медленном первом элементе")
    if bad:
        return bad
    return expect(call_with_timeout(lambda: m.bounded_pipeline([], lambda x: x)), [], "bounded_pipeline([], ...)")


def t_pipeline_bounded():
    created = []
    peak = []
    real = queue.Queue

    class SpyQueue(real):
        def __init__(self, maxsize=0):
            super().__init__(maxsize)
            created.append(maxsize)

        def put(self, item, block=True, timeout=None):
            super().put(item, block, timeout)
            peak.append(self.qsize())

    queue.Queue = SpyQueue
    try:
        out = call_with_timeout(lambda: m.bounded_pipeline(range(20), lambda x: x * 2, workers=2, capacity=3))
    finally:
        queue.Queue = real
    if created != [3]:
        return f"ожидалась одна очередь queue.Queue(maxsize=3), создано: {created}"
    if peak and max(peak) > 3:
        return f"размер очереди достигал {max(peak)} при capacity=3"
    return expect(out, [x * 2 for x in range(20)], "результат")


def t_pipeline_parallel_and_clean():
    before = threading.active_count()
    barrier = threading.Barrier(3, timeout=2)
    names = set()

    def tf(x):
        names.add(threading.current_thread().name)
        barrier.wait()
        return x

    out = call_with_timeout(lambda: m.bounded_pipeline([0, 1, 2], tf, workers=3, capacity=3))
    if out != [0, 1, 2]:
        return f"результат {out!r}, ожидалось [0, 1, 2]"
    if len(names) != 3:
        return f"элементы обработали {len(names)} потоков, ожидалось 3 одновременно работающих потребителя"
    for _ in range(100):
        if threading.active_count() <= before:
            return None
        time.sleep(0.01)
    return "после возврата остались живые потоки: нужно join для всех потребителей"


def t_pipeline_error():
    seen = []

    def tf(x):
        seen.append(x)
        if x in (3, 5):
            raise ValueError(f"bad {x}")
        return x

    try:
        call_with_timeout(lambda: m.bounded_pipeline(range(10), tf, workers=2, capacity=1))
    except ValueError as exc:
        if str(exc) != "bad 3":
            return f"поднято {exc!r}, ожидалась ошибка элемента с наименьшим индексом (bad 3)"
    else:
        return "исключение из transform должно быть поднято"
    if sorted(seen) != list(range(10)):
        return f"обработаны не все элементы: {sorted(seen)}"


CASES = [
    ("counter-basic", t_counter_basic),
    ("counter-uses-lock", t_counter_uses_lock),
    ("pipeline-basic", t_pipeline_basic),
    ("counter-threads", t_counter_threads),
    ("pipeline-bounded", t_pipeline_bounded),
    ("pipeline-parallel-and-clean", t_pipeline_parallel_and_clean),
    ("pipeline-error", t_pipeline_error),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
