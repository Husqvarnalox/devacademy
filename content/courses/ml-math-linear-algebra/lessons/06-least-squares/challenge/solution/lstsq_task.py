import numpy as np


def _with_bias(X):
    X = np.asarray(X, dtype=float)
    return np.column_stack([np.ones(len(X)), X])


def fit_lstsq(X, y):
    """Веса линейной регрессии w формы (d + 1,), w[0] - свободный член.

    X - массив (n, d), y - массив (n,). Добавьте столбец единиц слева и решите задачу
    наименьших квадратов через np.linalg.lstsq(..., rcond=None). Входные массивы не изменяйте.
    """
    Xb = _with_bias(X)
    w, *_ = np.linalg.lstsq(Xb, np.asarray(y, dtype=float), rcond=None)
    return w


def fit_ridge(X, y, alpha):
    """Гребневая регрессия: решите (Xb^T Xb + alpha * I') w = Xb^T y через np.linalg.solve.

    Xb - X со столбцом единиц слева; I' - единичная матрица, у которой на месте
    свободного члена (позиция 0, 0) стоит ноль. Верните w формы (d + 1,).
    """
    Xb = _with_bias(X)
    penalty = alpha * np.eye(Xb.shape[1])
    penalty[0, 0] = 0.0
    return np.linalg.solve(Xb.T @ Xb + penalty, Xb.T @ np.asarray(y, dtype=float))


def r2_score(y, y_pred):
    """Коэффициент детерминации 1 - SS_res / SS_tot.

    SS_res = sum((y - y_pred) ** 2), SS_tot = sum((y - mean(y)) ** 2).
    Если SS_tot == 0, верните 1.0 при SS_res == 0 и 0.0 иначе.
    """
    y = np.asarray(y, dtype=float)
    y_pred = np.asarray(y_pred, dtype=float)
    ss_res = float(np.sum((y - y_pred) ** 2))
    ss_tot = float(np.sum((y - y.mean()) ** 2))
    if ss_tot == 0.0:
        return 1.0 if ss_res == 0.0 else 0.0
    return 1.0 - ss_res / ss_tot
