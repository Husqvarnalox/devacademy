import functools


def retry(times, exceptions=(Exception,)):
    """Декоратор: повторять вызов до times раз при исключениях из exceptions."""
    pass


def memoize(func):
    """Декоратор: запоминать результаты по аргументам; у обёртки есть cache_clear()."""
    pass
