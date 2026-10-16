import itertools


def chunked(iterable, size):
    """Генератор списков по size элементов; лениво, работает с бесконечным входом."""
    if size < 1:
        raise ValueError("size должен быть >= 1")
    it = iter(iterable)
    while True:
        chunk = list(itertools.islice(it, size))
        if not chunk:
            return
        yield chunk


class Countdown:
    """Итератор: start, start-1, ..., 1."""

    def __init__(self, start):
        self.current = start

    def __iter__(self):
        return self

    def __next__(self):
        if self.current <= 0:
            raise StopIteration
        value = self.current
        self.current -= 1
        return value


def flatten(nested):
    """Лениво выдаёт листья вложенных списков и кортежей (строки - листья)."""
    for item in nested:
        if isinstance(item, (list, tuple)):
            yield from flatten(item)
        else:
            yield item


def run_lengths(items):
    """[(значение, длина серии), ...] для подряд идущих равных элементов."""
    return [(key, sum(1 for _ in group)) for key, group in itertools.groupby(items)]
