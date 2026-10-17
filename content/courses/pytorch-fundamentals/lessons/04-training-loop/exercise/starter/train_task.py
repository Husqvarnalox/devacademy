import torch
from torch import nn


def train_step(model, optimizer, loss_fn, xb, yb):
    """Один шаг обучения на пакете: обнулить градиенты, прямой проход, потеря, backward, шаг оптимизатора.

    Вернуть значение потери ДО шага как обычное число float.
    """
    # TODO
    return 0.0


def evaluate(model, loss_fn, X, y):
    """Потеря на данных без обучения: без построения графа и без изменения градиентов параметров. Вернуть float."""
    # TODO
    return 0.0


def train(model, optimizer, loss_fn, X, y, epochs):
    """Полнопакетное обучение: epochs раз вызвать train_step на всех данных. Вернуть список потерь (float) длины epochs."""
    # TODO
    return []


def fit_linear(X, y, lr, epochs):
    """Линейная регрессия на nn.Linear(d, 1): веса и смещение стартуют с нулей, SGD(lr), MSELoss.

    X формы (n, d) float32, y формы (n,). Вернуть (w, b, losses): w - тензор формы (d,) без градиента,
    b - float, losses - список потерь по эпохам.
    """
    # TODO
    return torch.zeros(X.shape[1]), 0.0, []
