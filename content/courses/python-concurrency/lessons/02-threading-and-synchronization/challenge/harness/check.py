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

import bank_task as m


class SpyLock:
    def __init__(self, log, name):
        self._lock = threading.Lock()
        self.log = log
        self.name = name

    def __enter__(self):
        self._lock.acquire()
        self.log.append(self.name)
        return self

    def __exit__(self, *exc):
        self._lock.release()

    def locked(self):
        return self._lock.locked()


def t_basic():
    a, b = m.Account(1, 100), m.Account(2, 50)
    m.transfer(a, b, 30)
    bad = expect((a.balance, b.balance), (70, 80), "балансы после transfer(a, b, 30)")
    if bad:
        return bad
    m.transfer(b, a, 80)
    return expect((a.balance, b.balance), (150, 0), "балансы после transfer(b, a, 80)")


def t_lock_order():
    for src_id, dst_id in ((1, 2), (2, 1)):
        log = []
        accounts = {1: m.Account(1, 10), 2: m.Account(2, 10)}
        for i, acc in accounts.items():
            acc.lock = SpyLock(log, i)
        call_with_timeout(lambda: m.transfer(accounts[src_id], accounts[dst_id], 5))
        if log != [1, 2]:
            return f"transfer({src_id}->{dst_id}) захватил блокировки в порядке {log}, ожидалось [1, 2]"


def t_errors_release():
    a, b = m.Account(1, 10), m.Account(2, 10)
    for args in ((a, b, 11), (a, b, 0), (b, a, -5)):
        try:
            call_with_timeout(lambda: m.transfer(*args))
        except ValueError:
            pass
        else:
            return f"transfer с суммой {args[2]} должен поднимать ValueError"
        if (a.balance, b.balance) != (10, 10):
            return "при ошибке баланс не должен меняться"
        if a.lock.locked() or b.lock.locked():
            return "после ошибки блокировка осталась захваченной"
    call_with_timeout(lambda: m.transfer(a, a, 5))
    bad = expect(a.balance, 10, "баланс после перевода на себя")
    if bad:
        return bad
    try:
        call_with_timeout(lambda: m.transfer(a, a, 50))
    except ValueError:
        return None
    return "перевод на себя большей суммы, чем баланс, тоже ValueError"


def t_opposite_threads():
    a, b = m.Account(1, 10_000), m.Account(2, 10_000)
    start = threading.Barrier(2, timeout=2)
    done = []
    failed = []

    def run(src, dst):
        try:
            start.wait()
            for _ in range(2000):
                m.transfer(src, dst, 1)
            done.append(1)
        except Exception as exc:
            failed.append(exc)

    ts = [threading.Thread(target=run, args=(a, b), daemon=True), threading.Thread(target=run, args=(b, a), daemon=True)]
    for t in ts:
        t.start()
    for t in ts:
        t.join(3)
    if failed:
        return f"исключение в потоке: {type(failed[0]).__name__}: {failed[0]}"
    if len(done) != 2:
        return "потоки не завершились: взаимная блокировка"
    return expect((a.balance, b.balance), (10_000, 10_000), "балансы после равного числа встречных переводов")


CASES = [
    ("transfer-basic", t_basic),
    ("lock-order", t_lock_order),
    ("errors-release", t_errors_release),
    ("opposite-threads", t_opposite_threads),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
