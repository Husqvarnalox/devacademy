import asyncio

STOP = object()


async def serve(queue, handler, workers, stop):
    """Вернуть (processed, remaining) после graceful shutdown."""
    processed = []

    async def next_item():
        getter = asyncio.ensure_future(queue.get())
        waiter = asyncio.ensure_future(stop.wait())
        try:
            await asyncio.wait({getter, waiter}, return_when=asyncio.FIRST_COMPLETED)
        finally:
            waiter.cancel()
            if not getter.done():
                getter.cancel()
        if getter.done() and not getter.cancelled():
            return getter.result()
        return STOP

    async def worker():
        while not stop.is_set():
            item = await next_item()
            if item is STOP:
                break
            try:
                await handler(item)
                processed.append(item)
            finally:
                queue.task_done()

    async with asyncio.TaskGroup() as tg:
        for _ in range(workers):
            tg.create_task(worker())

    remaining = []
    while True:
        try:
            remaining.append(queue.get_nowait())
        except asyncio.QueueEmpty:
            break
    return processed, remaining
