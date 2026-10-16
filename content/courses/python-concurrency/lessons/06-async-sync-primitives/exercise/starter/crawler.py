import asyncio


class AsyncCounter:
    def __init__(self):
        self.value = 0
        # TODO: добавить asyncio.Lock

    async def add(self, n=1):
        current = self.value
        await asyncio.sleep(0)          # имитация ввода-вывода между чтением и записью
        self.value = current + n
        return self.value


async def crawl(urls, fetch, max_concurrency):
    """url -> результат (или исключение), не более max_concurrency fetch одновременно."""
    # TODO
    return None


async def run_workers(items, handler, workers, queue_size):
    """Конвейер на asyncio.Queue(maxsize=queue_size); результаты в порядке items."""
    # TODO
    return None
