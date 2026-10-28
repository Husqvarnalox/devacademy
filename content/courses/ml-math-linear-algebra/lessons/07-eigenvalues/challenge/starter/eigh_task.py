import numpy as np


def covariance_matrix(X):
    """Выборочная ковариационная матрица признаков формы (d, d).

    X - массив (n, d), n >= 2. Вычтите среднее по каждому столбцу и вычислите
    Xc^T Xc / (n - 1). Входной массив не изменяйте.
    """
    # TODO
    d = np.asarray(X).shape[1]
    return np.zeros((d, d))


def sorted_eigh(S):
    """Собственные значения и векторы симметричной матрицы S по убыванию значений.

    Вернуть (vals, vecs): vals формы (d,), vecs формы (d, d), столбец vecs[:, i] -
    собственный вектор для vals[i]. Используйте np.linalg.eigh и переставьте результат.
    """
    # TODO
    d = np.asarray(S).shape[0]
    return np.zeros(d), np.eye(d)


def reconstruct(vals, vecs):
    """Матрица V diag(vals) V^T по собственным значениям и векторам-столбцам."""
    # TODO
    return np.zeros_like(vecs, dtype=float)
