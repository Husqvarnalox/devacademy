from operator import itemgetter


def compose(*funcs):
    """compose(f, g)(x) == f(g(x)); compose()(x) == x."""
    def run(x):
        for f in reversed(funcs):
            x = f(x)
        return x

    return run


def make_counter(start=0):
    """Возвращает функцию-счётчик: каждый вызов даёт на 1 больше предыдущего."""
    count = start

    def inc():
        nonlocal count
        count += 1
        return count

    return inc


def make_multipliers(n):
    """Список из n функций: i-я умножает аргумент на i."""
    return [lambda x, i=i: x * i for i in range(n)]


def sort_records(records, *fields):
    """Новая копия records, отсортированная по полям fields (itemgetter)."""
    if not fields:
        return list(records)
    return sorted(records, key=itemgetter(*fields))
