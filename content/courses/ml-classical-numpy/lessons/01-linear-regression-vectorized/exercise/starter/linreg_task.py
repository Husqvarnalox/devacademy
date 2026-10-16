import numpy as np


def add_bias(X):
    """Добавьте слева столбец единиц: X формы (n, d) -> (n, d + 1), float64."""
    # TODO
    return X


def mse(Xb, y, w):
    """Среднеквадратичная ошибка mean((Xb @ w - y)^2). Xb уже содержит столбец смещения. Результат - float."""
    # TODO
    return 0.0


def fit_normal(X, y):
    """Нормальные уравнения (Xb^T Xb) w = Xb^T y, решённые через np.linalg.solve (НЕ inv).

    X без смещения; верните w формы (d + 1,), w[0] - смещение.
    """
    # TODO
    return np.zeros(X.shape[1] + 1)


def fit_lstsq(X, y):
    """Метод наименьших квадратов через np.linalg.lstsq(Xb, y, rcond=None). Верните w формы (d + 1,), w[0] - смещение."""
    # TODO
    return np.zeros(X.shape[1] + 1)


def fit_gd(X, y, lr, epochs):
    """Пакетный градиентный спуск, начало w = 0. Градиент MSE: (2/n) * Xb^T (Xb @ w - y).

    Верните (w, history): history - массив длины epochs + 1 со значениями MSE до первого шага и после каждого шага.
    """
    # TODO
    return np.zeros(X.shape[1] + 1), np.zeros(epochs + 1)
