import numpy as np


def fit_lstsq(X, y):
    """Веса линейной регрессии w формы (d + 1,), w[0] - свободный член.

    X - массив (n, d), y - массив (n,). Добавьте столбец единиц слева и решите задачу
    наименьших квадратов через np.linalg.lstsq(..., rcond=None). Входные массивы не изменяйте.
    """
    # TODO
    return np.zeros(np.asarray(X).shape[1] + 1)


def fit_ridge(X, y, alpha):
    """Гребневая регрессия: решите (Xb^T Xb + alpha * I') w = Xb^T y через np.linalg.solve.

    Xb - X со столбцом единиц слева; I' - единичная матрица, у которой на месте
    свободного члена (позиция 0, 0) стоит ноль. Верните w формы (d + 1,).
    """
    # TODO
    return np.zeros(np.asarray(X).shape[1] + 1)


def r2_score(y, y_pred):
    """Коэффициент детерминации 1 - SS_res / SS_tot.

    SS_res = sum((y - y_pred) ** 2), SS_tot = sum((y - mean(y)) ** 2).
    Если SS_tot == 0, верните 1.0 при SS_res == 0 и 0.0 иначе.
    """
    # TODO
    return 0.0
