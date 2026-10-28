import numpy as np


def low_rank_approx(A, k):
    """Лучшее приближение матрицы A матрицей ранга не выше k (усечённое SVD).

    A - массив (m, n). Используйте np.linalg.svd(A, full_matrices=False) и оставьте k главных
    троек. Вернуть массив (m, n) типа float. ValueError, если k < 0 или k > min(m, n);
    при k = 0 - нулевая матрица. Входной массив не изменяйте.
    """
    # TODO
    return np.asarray(A, dtype=float).copy()


def approximation_error(A, k):
    """Норма Фробениуса разности A - A_k, где A_k = low_rank_approx(A, k)."""
    # TODO
    return 0.0


def choose_rank(A, energy):
    """Наименьшее k, для которого sum(s[:k] ** 2) / sum(s ** 2) >= energy (s - сингулярные числа A).

    0 < energy <= 1, иначе ValueError. Сравнивайте с допуском: ratio >= energy - 1e-12,
    чтобы energy = 1.0 не терялась из-за округления. Для нулевой матрицы верните 0.
    """
    # TODO
    return 0


def storage_ratio(m, n, k):
    """Во сколько раз k главных троек (U[:, :k], s[:k], V[:, :k]) занимают меньше места, чем матрица m x n.

    Формула: k * (m + n + 1) / (m * n).
    """
    # TODO
    return 0.0
