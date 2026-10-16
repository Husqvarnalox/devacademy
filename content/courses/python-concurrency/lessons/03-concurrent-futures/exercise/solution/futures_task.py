import pickle
from concurrent.futures import ThreadPoolExecutor, as_completed


def run_collect(func, items, max_workers=4):
    """Вернуть (values, errors): словари индекс -> результат и индекс -> исключение."""
    values = {}
    errors = {}
    with ThreadPoolExecutor(max_workers=max_workers) as pool:
        futures = {pool.submit(func, item): index for index, item in enumerate(items)}
        for future in as_completed(futures):
            index = futures[future]
            exc = future.exception()
            if exc is None:
                values[index] = future.result()
            else:
                errors[index] = exc
    return values, errors


def map_strict(func, items, max_workers=4):
    """Список результатов в порядке входа; при ошибке - исключение первого упавшего по порядку."""
    with ThreadPoolExecutor(max_workers=max_workers) as pool:
        return list(pool.map(func, items))


def check_picklable(obj):
    """True, если pickle.dumps(obj) проходит."""
    try:
        pickle.dumps(obj)
    except Exception:
        return False
    return True


def chunked(items, size):
    """Разбить items на списки по size элементов."""
    if size <= 0:
        raise ValueError("size must be positive")
    items = list(items)
    return [items[i:i + size] for i in range(0, len(items), size)]
