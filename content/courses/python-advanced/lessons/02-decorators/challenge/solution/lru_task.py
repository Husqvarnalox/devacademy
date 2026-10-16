import functools
from collections import OrderedDict


def lru(maxsize):
    """Декоратор: кеш на maxsize записей с вытеснением наименее недавно использованной."""
    if maxsize < 1:
        raise ValueError("maxsize должен быть >= 1")

    def decorator(func):
        cache = OrderedDict()
        hits = misses = 0

        @functools.wraps(func)
        def wrapper(*args):
            nonlocal hits, misses
            if args in cache:
                hits += 1
                cache.move_to_end(args)
                return cache[args]
            misses += 1
            value = func(*args)
            cache[args] = value
            if len(cache) > maxsize:
                cache.popitem(last=False)
            return value

        def info():
            return (hits, misses, len(cache))

        def clear():
            nonlocal hits, misses
            cache.clear()
            hits = misses = 0

        wrapper.info = info
        wrapper.clear = clear
        return wrapper

    return decorator
