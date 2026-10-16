import asyncio


async def worker(i, started, release, log):
    """Готовая корутина: менять не нужно."""
    log.append(f"start {i}")
    started.set()
    await release.wait()
    log.append(f"end {i}")


async def gather_ok(coros):
    """Конкурентно выполнить корутины в TaskGroup; результаты в порядке входа."""
    # TODO
    return None


async def reverse_finish(n):
    """Журнал: start 0..n-1, затем end n-1..0 (порядок - событиями)."""
    # TODO
    return None


async def run_blocking(func, *args, **kwargs):
    """Вызвать блокирующую func, не замораживая event loop."""
    # TODO
    return None
