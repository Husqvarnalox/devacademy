from collections.abc import Callable, Iterable, Sequence


def first_or[T](items: Sequence[T], default: T) -> T:
    """Первый элемент последовательности или default."""
    return items[0] if items else default


def group_by[T, K](items: Iterable[T], key: Callable[[T], K]) -> dict[K, list[T]]:
    """Сгруппировать элементы по ключу key(item)."""
    groups: dict[K, list[T]] = {}
    for item in items:
        groups.setdefault(key(item), []).append(item)
    return groups


def parse_pair(text: str) -> tuple[str, int] | None:
    """Разобрать 'имя=число' в (имя, число) или вернуть None."""
    name, sep, raw = text.partition("=")
    name = name.strip()
    if not sep or not name:
        return None
    try:
        return name, int(raw)
    except ValueError:
        return None
