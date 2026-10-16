import numpy as np


def pca_fit(X, k):
    """PCA через SVD центрированных данных.

    Возвращает (mean, components, explained_variance, explained_variance_ratio):
    mean (d,), components (k, d) - строки ортонормированы, explained_variance (k,) = s^2 / (n - 1), ratio (k,) - доли от полной дисперсии.
    """
    X = np.asarray(X, dtype=np.float64)
    n = X.shape[0]
    mean = X.mean(axis=0)
    U, s, Vt = np.linalg.svd(X - mean, full_matrices=False)
    var = s ** 2 / (n - 1)
    return mean, Vt[:k].copy(), var[:k].copy(), var[:k] / var.sum()


def pca_transform(X, mean, components):
    """Координаты в главных компонентах: (X - mean) @ components.T, форма (n, k)."""
    return (np.asarray(X, dtype=np.float64) - mean) @ components.T


def pca_reconstruct(Z, mean, components):
    """Обратное преобразование: Z @ components + mean, форма (n, d)."""
    return Z @ components + mean


def reconstruction_error(X, k):
    """Средняя по объектам сумма квадратов остатка после проекции на k компонент (float)."""
    X = np.asarray(X, dtype=np.float64)
    mean, comps, _, _ = pca_fit(X, k)
    R = pca_reconstruct(pca_transform(X, mean, comps), mean, comps)
    return float(np.mean(np.sum((X - R) ** 2, axis=1)))
