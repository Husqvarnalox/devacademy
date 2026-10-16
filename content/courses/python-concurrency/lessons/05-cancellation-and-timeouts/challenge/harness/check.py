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

import commit_task as m


def t_commit_normal():
    async def main():
        async def commit():
            await asyncio.sleep(0)
            return "tx-1"

        log = []
        return await m.shielded_commit(commit, log), log

    return expect(run_async(main), ("tx-1", []), "shielded_commit без отмены")


async def scenario(release_error=None):
    log = []
    started, release = asyncio.Event(), asyncio.Event()
    box = {}

    async def commit():
        log.append("commit start")
        started.set()
        await release.wait()
        log.append("committed")
        return "tx"

    task = asyncio.create_task(m.shielded_commit(commit, log))
    await started.wait()
    inner = [t for t in asyncio.all_tasks() if t is not task and t is not asyncio.current_task()]
    task.cancel()
    for _ in range(5):
        await asyncio.sleep(0)
    box["done_before_release"] = task.done()
    release.set()
    try:
        await task
        box["outcome"] = "returned"
    except asyncio.CancelledError:
        box["outcome"] = "cancelled"
    box["task_cancelled"] = task.cancelled()
    box["inner_cancelled"] = any(t.cancelled() for t in inner)
    box["log"] = log
    return box


def t_survives_cancel():
    box = run_async(scenario)
    return expect(
        (box["done_before_release"], box["outcome"], box["task_cancelled"], box["log"]),
        (False, "cancelled", True, ["commit start", "committed", "cancelled after commit"]),
        "shielded_commit при отмене: (завершилась до фиксации, исход, cancelled, журнал)",
    )


def t_inner_not_cancelled():
    box = run_async(scenario)
    if box["inner_cancelled"]:
        return "внутренняя задача фиксации была отменена"
    if "committed" not in box["log"]:
        return "фиксация не завершилась"


def t_commit_error():
    async def main():
        async def commit():
            await asyncio.sleep(0)
            raise RuntimeError("db down")

        try:
            await m.shielded_commit(commit, [])
        except RuntimeError as exc:
            return str(exc)
        return "no error"

    return expect(run_async(main), "db down", "ошибка commit без отмены")


CASES = [
    ("commit-normal", t_commit_normal),
    ("commit-survives-cancel", t_survives_cancel),
    ("inner-not-cancelled", t_inner_not_cancelled),
    ("commit-error", t_commit_error),
]


if __name__ == "__main__":
    for _id, _check in CASES:
        case(_id, _check)
    # leaked non-daemon threads of a faulty solution must not keep the harness alive
    os._exit(0)
