import functools


def call_counter(func):
    """Обёртка с атрибутом calls - числом вызовов."""
    @functools.wraps(func)
    def wrapper(*args, **kwargs):
        wrapper.calls += 1
        return func(*args, **kwargs)

    wrapper.calls = 0
    return wrapper


def retry(times, exceptions=(Exception,)):
    """До times попыток; после последней неудачи - повторно возбудить исключение."""
    def decorator(func):
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            last = None
            for _ in range(times):
                try:
                    return func(*args, **kwargs)
                except exceptions as exc:
                    last = exc
            raise last

        return wrapper

    return decorator


def memoize(func):
    """Кеш по позиционным аргументам, доступен как wrapper.cache (dict)."""
    @functools.wraps(func)
    def wrapper(*args):
        if args not in wrapper.cache:
            wrapper.cache[args] = func(*args)
        return wrapper.cache[args]

    wrapper.cache = {}
    return wrapper


def tag(name):
    """Оборачивает строковый результат в <name>...</name>."""
    def decorator(func):
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            return f"<{name}>{func(*args, **kwargs)}</{name}>"

        return wrapper

    return decorator
