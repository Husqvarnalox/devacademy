import queue
import threading


class SafeCounter:
    def __init__(self, lock_factory=threading.Lock):
        self._lock = lock_factory()
        self._value = 0

    def increment(self, n=1):
        with self._lock:
            self._value += n
            return self._value

    @property
    def value(self):
        with self._lock:
            return self._value

    def increment_if_below(self, limit):
        with self._lock:
            if self._value < limit:
                self._value += 1
                return True
            return False


def bounded_pipeline(items, transform, workers=3, capacity=2):
    """Ограниченный producer/consumer; результаты в порядке items."""
    items = list(items)
    jobs = queue.Queue(maxsize=capacity)
    results = [None] * len(items)
    errors = {}

    def consume():
        while True:
            job = jobs.get()
            if job is None:
                return
            index, item = job
            try:
                results[index] = transform(item)
            except Exception as exc:
                errors[index] = exc

    threads = [threading.Thread(target=consume) for _ in range(workers)]
    for t in threads:
        t.start()
    for job in enumerate(items):
        jobs.put(job)
    for _ in threads:
        jobs.put(None)
    for t in threads:
        t.join()
    if errors:
        raise errors[min(errors)]
    return results
