import numpy as np


def row_view(a, i):
    """Верните i-ю строку 2D-массива a как ПРЕДСТАВЛЕНИЕ: запись в результат должна менять a."""
    # TODO
    return a.copy()


def step_view(a, step):
    """Верните представление каждого step-го элемента 1D-массива a (a[::step]) без копирования."""
    # TODO
    return a.copy()


def positives_copy(a):
    """Верните новый массив только из положительных элементов a (КОПИЮ, не связанную с a)."""
    # TODO
    return a


def clip_negative(a):
    """Верните массив, где отрицательные элементы заменены на 0. Входной массив менять НЕЛЬЗЯ."""
    # TODO
    return a


def reorder_columns(a, order):
    """Верните копию 2D-массива a со столбцами в порядке order (список индексов)."""
    # TODO
    return a
