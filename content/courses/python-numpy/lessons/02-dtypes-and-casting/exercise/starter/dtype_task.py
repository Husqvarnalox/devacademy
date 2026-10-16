import numpy as np


def wrap_add(a, b, dtype):
    """Сложите массивы a и b, предварительно приведя их к dtype (например "int8").

    Переполнение допускается и должно «заворачиваться» так же, как в самом dtype.
    Верните ndarray указанного dtype.
    """
    # TODO
    return None


def widen_add(a, b):
    """Сложите два целочисленных массива так, чтобы переполнения не было.

    Верните ndarray с dtype int64.
    """
    # TODO
    return None


def to_uint8(x):
    """Переведите массив вещественных значений из диапазона [0, 1] в uint8 (яркость 0..255).

    Значения вне [0, 1] обрезайте до границ, округляйте до ближайшего целого (rint).
    """
    # TODO
    return None


def promoted_dtype(a, b):
    """Верните имя dtype (строкой), в который NumPy приведёт сумму массивов dtype a и dtype b."""
    # TODO
    return ""
