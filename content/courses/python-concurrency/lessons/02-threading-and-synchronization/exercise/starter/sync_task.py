import queue
import threading


class SafeCounter:
    def __init__(self, lock_factory=threading.Lock):
        # TODO: создать блокировку через lock_factory и начальное значение
        pass

    def increment(self, n=1):
        """Прибавить n, вернуть новое значение."""
        # TODO
        return None

    @property
    def value(self):
        # TODO
        return None

    def increment_if_below(self, limit):
        """Атомарно: если value < limit - прибавить 1 и вернуть True, иначе False."""
        # TODO
        return None


def bounded_pipeline(items, transform, workers=3, capacity=2):
    """Ограниченный producer/consumer; результаты в порядке items."""
    # TODO
    return None
