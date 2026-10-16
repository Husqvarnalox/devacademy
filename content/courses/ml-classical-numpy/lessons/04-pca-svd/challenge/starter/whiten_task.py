import numpy as np


def whiten_fit_transform(X, k):
    """Отбеливание: центрирование, SVD, проекция на k компонент, деление каждого столбца на sqrt(дисперсии ev = s^2 / (n - 1)).

    Верните (Z, mean, components, ev); Z (n, k) с единичной выборочной дисперсией (ddof=1) в каждом столбце.
    Если ev[j] <= 1e-12 * max(ev), делитель равен 1.0 (иначе будет nan/inf на вырожденных данных).
    """
    # TODO
    d = X.shape[1]
    return np.zeros((X.shape[0], k)), np.zeros(d), np.zeros((k, d)), np.zeros(k)


def unwhiten(Z, mean, components, ev):
    """Обратное преобразование для Z из whiten_fit_transform: (Z * делители) @ components + mean; делители те же, что при отбеливании."""
    # TODO
    return np.zeros((Z.shape[0], components.shape[1]))
