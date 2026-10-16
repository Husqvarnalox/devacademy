import numpy as np


def col_means(a):
    """Среднее по каждому столбцу 2D-массива a: форма (число столбцов,), float64."""
    # TODO
    return a


def row_argmax(a):
    """Для каждой строки 2D-массива a индекс максимального элемента (первого при равенстве): целые, форма (число строк,)."""
    # TODO
    return a


def stable_softmax(a):
    """Softmax по строкам 2D-массива a: exp(a - max) / сумма. Должен работать на больших значениях (1000 и больше) без nan/inf.

    Результат float64 той же формы, каждая строка суммируется в 1.
    """
    # TODO
    return a


def variance(a, ddof=0):
    """Дисперсия 1D-массива a: sum((a - mean)^2) / (n - ddof), БЕЗ np.var и без формулы E[x^2] - E[x]^2.

    Если n - ddof <= 0, бросьте ValueError. Результат - float.
    """
    # TODO
    return 0.0
