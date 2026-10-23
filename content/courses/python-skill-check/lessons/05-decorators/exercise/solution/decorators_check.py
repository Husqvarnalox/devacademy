import functools


def retry(times, exceptions=(Exception,)):
    """Декоратор: повторять вызов до times раз при исключениях из exceptions."""
    if times < 1:
        raise ValueError("times должно быть не меньше 1")

    def decorator(func):
        @functools.wraps(func)
        def wrapper(*args, **kwargs):
            for attempt in range(times):
                try:
                    return func(*args, **kwargs)
                except exceptions:
                    if attempt == times - 1:
                        raise
        return wrapper

    return decorator


def memoize(func):
    """Декоратор: запоминать результаты по аргументам; у обёртки есть cache_clear()."""
    cache = {}

    @functools.wraps(func)
    def wrapper(*args, **kwargs):
        key = (args, tuple(sorted(kwargs.items())))
        if key not in cache:
            cache[key] = func(*args, **kwargs)
        return cache[key]

    wrapper.cache_clear = cache.clear
    return wrapper
