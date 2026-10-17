import numpy as np
import torch


def to_float_matrix(rows):
    """Список списков чисел -> тензор формы (n, d) dtype float32."""
    # TODO
    return torch.tensor(rows)


def standardize_columns(x):
    """Стандартизация по столбцам: (x - среднее) / std, где std - population (unbiased=False).

    Столбец с нулевым std должен стать нулевым (деление на 1, а не на 0). Без циклов; вход не менять.
    """
    # TODO
    return x


def pairwise_sq_dists(a, b):
    """a формы (n, d), b формы (m, d) -> матрица (n, m) квадратов евклидовых расстояний. Без циклов, через broadcasting."""
    # TODO
    return torch.zeros(a.shape[0], b.shape[0])


def share_with_numpy(t):
    """Вернуть numpy-массив, который ДЕЛИТ память с тензором t (изменение одного видно в другом)."""
    # TODO
    return t.tolist()


def copy_from_numpy(arr):
    """Вернуть float32-тензор с данными arr, НЕ делящий память с arr (даже если arr уже float32)."""
    # TODO
    return torch.from_numpy(arr)
