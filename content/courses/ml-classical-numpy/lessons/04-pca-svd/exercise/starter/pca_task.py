import numpy as np


def pca_fit(X, k):
    """PCA через SVD центрированных данных (np.linalg.svd(Xc, full_matrices=False)).

    Верните (mean, components, explained_variance, explained_variance_ratio):
    mean (d,); components (k, d) - первые k строк Vt; explained_variance (k,) = s^2 / (n - 1);
    ratio (k,) - explained_variance, делённая на сумму дисперсий ВСЕХ компонент.
    """
    # TODO
    d = X.shape[1]
    return np.zeros(d), np.zeros((k, d)), np.zeros(k), np.zeros(k)


def pca_transform(X, mean, components):
    """Координаты в главных компонентах: (X - mean) @ components.T, форма (n, k)."""
    # TODO
    return np.zeros((X.shape[0], components.shape[0]))


def pca_reconstruct(Z, mean, components):
    """Обратное преобразование: Z @ components + mean, форма (n, d)."""
    # TODO
    return np.zeros((Z.shape[0], components.shape[1]))


def reconstruction_error(X, k):
    """Средняя по объектам сумма квадратов остатка после проекции на k компонент: mean(sum((X - R)^2, axis=1)). Верните float."""
    # TODO
    return 0.0
