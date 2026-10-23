from collections import deque


def parse_ints(lines):
    """Выдавать int для каждой непустой строки."""
    for line in lines:
        text = line.strip()
        if text:
            yield int(text)


def window(iterable, size):
    """Выдавать кортежи из size подряд идущих элементов."""
    buffer = deque(maxlen=size)
    for item in iterable:
        buffer.append(item)
        if len(buffer) == size:
            yield tuple(buffer)


def running_total(iterable):
    """Выдавать накопленные суммы."""
    total = 0
    for value in iterable:
        total += value
        yield total
