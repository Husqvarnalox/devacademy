import numpy as np


def batch_dense(X, W, b):
    """Слой для пакета объектов: X @ W.T + b.

    X - массив (n, d_in) (объекты в строках), W - (d_out, d_in) (нейрон в строке), b - (d_out,).
    Верните массив (n, d_out). Если формы не согласуются (не 2D/1D, d_in не совпадает, длина b не равна d_out),
    выбросьте ValueError.
    """
    # TODO
    return np.zeros((len(X), len(W)))


def rotation_matrix(theta):
    """Матрица 2 x 2 поворота на угол theta (в радианах) против часовой стрелки."""
    # TODO
    return np.eye(2)


def transform_points(A, P):
    """Применить матрицу A к каждой точке: P - массив (n, 2), точки лежат строками.

    Верните массив, в котором строка i равна A @ P[i], то есть P @ A.T. Входные массивы не изменяйте.
    """
    # TODO
    return np.array(P, dtype=float)
