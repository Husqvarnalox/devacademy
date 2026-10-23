def wrap_index(i, n):
    """Вернуть позицию в кольце длины n для индекса i (отрицательные - с конца)."""
    return i % n


def first_present(values, default=None):
    """Вернуть первый элемент, не равный None и не равный пустой строке."""
    for value in values:
        if value is not None and value != "":
            return value
    return default


def factorial_digit_sum(n):
    """Вернуть сумму цифр n! (факториала)."""
    product = 1
    for k in range(2, n + 1):
        product *= k
    return sum(int(ch) for ch in str(product))


def deltas(values):
    """Вернуть список разностей между соседними элементами."""
    return [b - a for a, b in zip(values, values[1:])]


def numbered(items, start=1):
    """Вернуть список строк '<номер>. <элемент>'."""
    return [f"{i}. {item}" for i, item in enumerate(items, start=start)]
