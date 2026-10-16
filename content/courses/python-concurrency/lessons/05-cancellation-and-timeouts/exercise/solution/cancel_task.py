import asyncio
from collections import Counter


async def run_with_timeout(func, seconds):
    """('ok', результат) или ('timeout', None)."""
    try:
        async with asyncio.timeout(seconds):
            return "ok", await func()
    except TimeoutError:
        return "timeout", None


async def worker_with_cleanup(work, log):
    """acquire -> await work() -> release (всегда)."""
    log.append("acquire")
    try:
        return await work()
    finally:
        log.append("release")


async def cancel_and_wait(task):
    """Отменить задачу, дождаться, вернуть True, если она завершилась отменённой."""
    task.cancel()
    try:
        await task
    except asyncio.CancelledError:
        pass
    except Exception:
        pass
    return task.cancelled()


async def collect_errors(funcs):
    """{имя типа ошибки: количество} по упавшим задачам TaskGroup."""
    counts = Counter()
    try:
        async with asyncio.TaskGroup() as tg:
            for func in funcs:
                tg.create_task(func())
    except* Exception as group:
        for exc in group.exceptions:
            counts[type(exc).__name__] += 1
    return dict(sorted(counts.items()))
