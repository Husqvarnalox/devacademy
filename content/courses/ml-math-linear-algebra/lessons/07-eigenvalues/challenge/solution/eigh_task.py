import numpy as np


def covariance_matrix(X):
    """Выборочная ковариационная матрица признаков формы (d, d).

    X - массив (n, d), n >= 2. Вычтите среднее по каждому столбцу и вычислите
    Xc^T Xc / (n - 1). Входной массив не изменяйте.
    """
    X = np.asarray(X, dtype=float)
    Xc = X - X.mean(axis=0)
    return Xc.T @ Xc / (len(X) - 1)


def sorted_eigh(S):
    """Собственные значения и векторы симметричной матрицы S по убыванию значений.

    Вернуть (vals, vecs): vals формы (d,), vecs формы (d, d), столбец vecs[:, i] -
    собственный вектор для vals[i]. Используйте np.linalg.eigh и переставьте результат.
    """
    vals, vecs = np.linalg.eigh(np.asarray(S, dtype=float))
    order = np.argsort(vals)[::-1]
    return vals[order], vecs[:, order]


def reconstruct(vals, vecs):
    """Матрица V diag(vals) V^T по собственным значениям и векторам-столбцам."""
    return (vecs * vals) @ vecs.T
