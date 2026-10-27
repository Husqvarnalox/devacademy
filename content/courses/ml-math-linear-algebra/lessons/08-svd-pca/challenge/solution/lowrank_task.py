import numpy as np


def low_rank_approx(A, k):
    """Лучшее приближение матрицы A матрицей ранга не выше k (усечённое SVD).

    A - массив (m, n). Используйте np.linalg.svd(A, full_matrices=False) и оставьте k главных
    троек. Вернуть массив (m, n) типа float. ValueError, если k < 0 или k > min(m, n);
    при k = 0 - нулевая матрица. Входной массив не изменяйте.
    """
    A = np.asarray(A, dtype=float)
    if k < 0 or k > min(A.shape):
        raise ValueError("k должно быть от 0 до min(m, n)")
    if k == 0:
        return np.zeros_like(A)
    U, s, Vt = np.linalg.svd(A, full_matrices=False)
    return (U[:, :k] * s[:k]) @ Vt[:k]


def approximation_error(A, k):
    """Норма Фробениуса разности A - A_k, где A_k = low_rank_approx(A, k)."""
    A = np.asarray(A, dtype=float)
    return float(np.linalg.norm(A - low_rank_approx(A, k), "fro"))


def choose_rank(A, energy):
    """Наименьшее k, для которого sum(s[:k] ** 2) / sum(s ** 2) >= energy (s - сингулярные числа A).

    0 < energy <= 1, иначе ValueError. Сравнивайте с допуском: ratio >= energy - 1e-12,
    чтобы energy = 1.0 не терялась из-за округления. Для нулевой матрицы верните 0.
    """
    if not 0.0 < energy <= 1.0:
        raise ValueError("energy должна лежать в (0, 1]")
    s = np.linalg.svd(np.asarray(A, dtype=float), compute_uv=False)
    total = float(np.sum(s ** 2))
    if total == 0.0:
        return 0
    ratio = np.cumsum(s ** 2) / total
    return int(np.argmax(ratio >= energy - 1e-12)) + 1


def storage_ratio(m, n, k):
    """Во сколько раз k главных троек (U[:, :k], s[:k], V[:, :k]) занимают меньше места, чем матрица m x n.

    Формула: k * (m + n + 1) / (m * n).
    """
    return k * (m + n + 1) / (m * n)
