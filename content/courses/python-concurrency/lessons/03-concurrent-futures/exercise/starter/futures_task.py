import pickle
from concurrent.futures import ThreadPoolExecutor, as_completed


def run_collect(func, items, max_workers=4):
    """Вернуть (values, errors): словари индекс -> результат и индекс -> исключение."""
    # TODO
    return None


def map_strict(func, items, max_workers=4):
    """Список результатов в порядке входа; при ошибке - исключение первого упавшего по порядку."""
    # TODO
    return None


def check_picklable(obj):
    """True, если pickle.dumps(obj) проходит."""
    # TODO
    return None


def chunked(items, size):
    """Разбить items на списки по size элементов."""
    # TODO
    return None
