import itertools


def chunked(iterable, size):
    """Генератор списков по size элементов; лениво, работает с бесконечным входом."""
    # TODO
    return iter(())


class Countdown:
    """Итератор: start, start-1, ..., 1."""

    def __init__(self, start):
        self.current = start

    # TODO: реализуйте протокол итератора (__iter__ и __next__)


def flatten(nested):
    """Лениво выдаёт листья вложенных списков и кортежей (строки - листья)."""
    # TODO: используйте yield from
    return iter(())


def run_lengths(items):
    """[(значение, длина серии), ...] для подряд идущих равных элементов."""
    # TODO: itertools.groupby
    return None
