import asyncio


class AsyncCounter:
    def __init__(self):
        self.value = 0
        self._lock = asyncio.Lock()

    async def add(self, n=1):
        async with self._lock:
            current = self.value
            await asyncio.sleep(0)          # имитация ввода-вывода между чтением и записью
            self.value = current + n
            return self.value


async def crawl(urls, fetch, max_concurrency):
    """url -> результат (или исключение), не более max_concurrency fetch одновременно."""
    if max_concurrency < 1:
        raise ValueError("max_concurrency must be >= 1")
    urls = list(urls)
    sem = asyncio.Semaphore(max_concurrency)

    async def one(url):
        async with sem:
            try:
                return await fetch(url)
            except Exception as exc:
                return exc

    async with asyncio.TaskGroup() as tg:
        tasks = [tg.create_task(one(url)) for url in urls]
    return {url: task.result() for url, task in zip(urls, tasks)}


async def run_workers(items, handler, workers, queue_size):
    """Конвейер на asyncio.Queue(maxsize=queue_size); результаты в порядке items."""
    items = list(items)
    results = [None] * len(items)
    queue = asyncio.Queue(maxsize=queue_size)

    async def worker():
        while True:
            index, item = await queue.get()
            try:
                results[index] = await handler(item)
            except Exception as exc:
                results[index] = exc
            finally:
                queue.task_done()

    async with asyncio.TaskGroup() as tg:
        pool = [tg.create_task(worker()) for _ in range(workers)]
        for job in enumerate(items):
            await queue.put(job)
        await queue.join()
        for task in pool:
            task.cancel()
    return results
