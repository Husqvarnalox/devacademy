import functools
from collections import OrderedDict


def lru(maxsize):
    """Декоратор: кеш на maxsize записей с вытеснением наименее недавно использованной."""
    # TODO: проверьте maxsize и верните декоратор, добавляющий info() и clear()
    def decorator(func):
        return func

    return decorator
