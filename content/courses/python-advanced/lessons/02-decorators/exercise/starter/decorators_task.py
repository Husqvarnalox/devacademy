import functools


def call_counter(func):
    """Обёртка с атрибутом calls - числом вызовов."""
    # TODO
    return func


def retry(times, exceptions=(Exception,)):
    """До times попыток; после последней неудачи - повторно возбудить исключение."""
    # TODO
    def decorator(func):
        return func

    return decorator


def memoize(func):
    """Кеш по позиционным аргументам, доступен как wrapper.cache (dict)."""
    # TODO
    return func


def tag(name):
    """Оборачивает строковый результат в <name>...</name>."""
    # TODO
    def decorator(func):
        return func

    return decorator
