import numpy as np


def pairwise_sq_dists(A, B):
    """Матрица квадратов евклидовых расстояний D[i, j] = ||A[i] - B[j]||^2.

    A имеет форму (n, d), B - (m, d); результат float64 формы (n, m), без циклов Python
    и с неотрицательными значениями. Входы менять нельзя.
    """
    # TODO
    return np.zeros((len(A), len(B)))


def nearest_index(A, B):
    """Для каждой строки A индекс ближайшей строки B (при равенстве - наименьший индекс). Результат: массив int формы (n,)."""
    # TODO
    return np.zeros(len(A), dtype=np.int64)
