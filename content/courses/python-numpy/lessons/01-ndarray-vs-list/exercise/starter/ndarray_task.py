import numpy as np


def describe(a):
    """Верните словарь с ключами shape, dtype, itemsize, nbytes, strides, c_contiguous.

    shape и strides - кортежи, dtype - строка (например "int64"), остальное - обычные Python-значения.
    """
    # TODO
    return {}


def scale_all(values, k):
    """Умножьте каждый элемент values (список или ndarray) на число k.

    Верните ndarray с dtype float64 той же формы; входные данные не изменяйте.
    """
    # TODO
    return values


def c_strides(shape, itemsize):
    """Вычислите strides (в байтах) C-непрерывного массива заданной формы без обращения к NumPy.

    Для shape=() верните (). Результат - кортеж из int.
    """
    # TODO
    return ()
