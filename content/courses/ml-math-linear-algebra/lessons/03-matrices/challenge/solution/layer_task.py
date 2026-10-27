import numpy as np


def batch_dense(X, W, b):
    """Слой для пакета объектов: X @ W.T + b.

    X - массив (n, d_in) (объекты в строках), W - (d_out, d_in) (нейрон в строке), b - (d_out,).
    Верните массив (n, d_out). Если формы не согласуются (не 2D/1D, d_in не совпадает, длина b не равна d_out),
    выбросьте ValueError.
    """
    X = np.asarray(X, dtype=float)
    W = np.asarray(W, dtype=float)
    b = np.asarray(b, dtype=float)
    if X.ndim != 2 or W.ndim != 2 or b.ndim != 1:
        raise ValueError("ожидаются X (n, d_in), W (d_out, d_in), b (d_out,)")
    if X.shape[1] != W.shape[1]:
        raise ValueError(f"признаков в X: {X.shape[1]}, входов в W: {W.shape[1]}")
    if b.shape[0] != W.shape[0]:
        raise ValueError(f"нейронов в W: {W.shape[0]}, длина b: {b.shape[0]}")
    return X @ W.T + b


def rotation_matrix(theta):
    """Матрица 2 x 2 поворота на угол theta (в радианах) против часовой стрелки."""
    c, s = np.cos(theta), np.sin(theta)
    return np.array([[c, -s], [s, c]])


def transform_points(A, P):
    """Применить матрицу A к каждой точке: P - массив (n, 2), точки лежат строками.

    Верните массив, в котором строка i равна A @ P[i], то есть P @ A.T. Входные массивы не изменяйте.
    """
    return np.asarray(P, dtype=float) @ np.asarray(A, dtype=float).T
