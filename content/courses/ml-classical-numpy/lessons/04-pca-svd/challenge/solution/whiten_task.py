import numpy as np


def whiten_fit_transform(X, k):
    """Отбеливание: центрирование, проекция на k главных компонент и деление на sqrt(дисперсии).

    Возвращает (Z, mean, components, ev); Z (n, k), у каждого столбца выборочная дисперсия (ddof=1) равна 1.
    Компонента с дисперсией <= 1e-12 * max(ev) не масштабируется (делитель 1), чтобы не получить nan/inf.
    """
    X = np.asarray(X, dtype=np.float64)
    n = X.shape[0]
    mean = X.mean(axis=0)
    U, s, Vt = np.linalg.svd(X - mean, full_matrices=False)
    ev = (s ** 2 / (n - 1))[:k]
    comps = Vt[:k].copy()
    scale = np.where(ev > 1e-12 * ev.max(), np.sqrt(ev), 1.0) if ev.max() > 0 else np.ones_like(ev)
    Z = (X - mean) @ comps.T / scale
    return Z, mean, comps, ev


def unwhiten(Z, mean, components, ev):
    """Обратное преобразование для Z из whiten_fit_transform: восстановить X в исходном пространстве."""
    scale = np.where(ev > 1e-12 * ev.max(), np.sqrt(ev), 1.0) if ev.max() > 0 else np.ones_like(ev)
    return (Z * scale) @ components + mean
