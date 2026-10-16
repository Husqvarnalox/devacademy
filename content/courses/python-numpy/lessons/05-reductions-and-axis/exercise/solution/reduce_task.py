import numpy as np


def col_means(a):
    return np.asarray(a, dtype=np.float64).mean(axis=0)


def row_argmax(a):
    return np.argmax(a, axis=1)


def stable_softmax(a):
    a = np.asarray(a, dtype=np.float64)
    shifted = a - a.max(axis=1, keepdims=True)
    e = np.exp(shifted)
    return e / e.sum(axis=1, keepdims=True)


def variance(a, ddof=0):
    a = np.asarray(a, dtype=np.float64)
    n = a.shape[0]
    if n - ddof <= 0:
        raise ValueError("n - ddof должно быть положительным")
    centered = a - a.mean()
    return float((centered * centered).sum() / (n - ddof))
