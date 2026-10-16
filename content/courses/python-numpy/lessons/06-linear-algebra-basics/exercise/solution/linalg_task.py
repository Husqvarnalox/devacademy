import numpy as np


def matmul_shape(s1, s2):
    s1, s2 = tuple(s1), tuple(s2)
    if len(s1) not in (1, 2) or len(s2) not in (1, 2):
        raise ValueError("поддерживаются только 1D и 2D формы")
    inner1 = s1[-1]
    inner2 = s2[0]
    if inner1 != inner2:
        raise ValueError(f"внутренние размеры {inner1} и {inner2} не совпадают")
    return tuple(s1[:-1]) + tuple(s2[1:])


def affine(X, W, b):
    X = np.asarray(X, dtype=np.float64)
    W = np.asarray(W, dtype=np.float64)
    b = np.asarray(b, dtype=np.float64)
    return X @ W + b


def solve_system(A, b):
    return np.linalg.solve(np.asarray(A, dtype=np.float64), np.asarray(b, dtype=np.float64))


def row_norms(a, ord=2):
    return np.linalg.norm(np.asarray(a, dtype=np.float64), ord=ord, axis=1)


def normalize_rows(a):
    a = np.asarray(a, dtype=np.float64)
    norms = np.linalg.norm(a, axis=1, keepdims=True)
    return np.divide(a, norms, out=np.zeros_like(a), where=norms != 0)
