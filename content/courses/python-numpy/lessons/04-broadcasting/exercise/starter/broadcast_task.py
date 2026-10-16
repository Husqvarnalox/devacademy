import numpy as np


def broadcast_shape(s1, s2):
    """Форма результата broadcasting для форм s1 и s2 (кортежи) - по правилам, БЕЗ np.broadcast_shapes.

    Если формы несовместимы, бросьте ValueError.
    """
    # TODO
    return ()


def center_columns(a):
    """Вычтите из каждого столбца 2D-массива a его среднее. Результат той же формы, float64."""
    # TODO
    return a


def row_normalize(a):
    """Поделите каждую строку 2D-массива a на сумму её элементов (строки с нулевой суммой остаются нулевыми).

    Результат float64 той же формы.
    """
    # TODO
    return a


def outer_sum(x, y):
    """Для 1D-массивов x (длины n) и y (длины m) верните массив формы (n, m) с элементами x[i] + y[j]."""
    # TODO
    return x
