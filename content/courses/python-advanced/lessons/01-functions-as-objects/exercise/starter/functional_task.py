from operator import itemgetter


def compose(*funcs):
    """compose(f, g)(x) == f(g(x)); compose()(x) == x."""
    # TODO
    pass


def make_counter(start=0):
    """Возвращает функцию-счётчик: каждый вызов даёт на 1 больше предыдущего."""
    count = start

    def inc():
        # TODO: изменить count внешней функции
        count += 1
        return count

    return inc


def make_multipliers(n):
    """Список из n функций: i-я умножает аргумент на i."""
    # TODO: в таком виде все функции умножают на одно и то же число - исправьте
    return [lambda x: x * i for i in range(n)]


def sort_records(records, *fields):
    """Новая копия records, отсортированная по полям fields (itemgetter)."""
    # TODO
    pass
