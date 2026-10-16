import asyncio


async def worker(i, started, release, log):
    """Готовая корутина: менять не нужно."""
    log.append(f"start {i}")
    started.set()
    await release.wait()
    log.append(f"end {i}")


async def gather_ok(coros):
    """Конкурентно выполнить корутины в TaskGroup; результаты в порядке входа."""
    async with asyncio.TaskGroup() as tg:
        tasks = [tg.create_task(c) for c in coros]
    return [t.result() for t in tasks]


async def reverse_finish(n):
    """Журнал: start 0..n-1, затем end n-1..0 (порядок - событиями)."""
    log = []
    started = [asyncio.Event() for _ in range(n)]
    release = [asyncio.Event() for _ in range(n)]
    tasks = [asyncio.create_task(worker(i, started[i], release[i], log)) for i in range(n)]
    for event in started:
        await event.wait()
    for i in reversed(range(n)):
        release[i].set()
        await tasks[i]
    return log


async def run_blocking(func, *args, **kwargs):
    """Вызвать блокирующую func, не замораживая event loop."""
    return await asyncio.to_thread(func, *args, **kwargs)
