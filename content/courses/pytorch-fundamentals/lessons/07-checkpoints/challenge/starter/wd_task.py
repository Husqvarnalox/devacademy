import torch
from torch import nn


def train_regularized(X, y, hidden, weight_decay, epochs, lr, seed):
    """Регрессия сетью Linear(d, hidden) -> ReLU -> Linear(hidden, 1) на полном пакете.

    torch.manual_seed(seed) ДО создания сети; SGD(lr, weight_decay=weight_decay), потеря MSELoss;
    выход сети приводится к форме (n,). Вернуть обученную модель.
    """
    # TODO
    return nn.Identity()


def weight_norm(model):
    """L2-норма всех параметров-матриц весов (тензоры с dim() >= 2, смещения не входят) как float."""
    # TODO
    return 0.0


def mse(model, X, y):
    """Среднеквадратичная ошибка модели на (X, y) без градиентов, float."""
    # TODO
    return 0.0


def compare_weight_decay(X, y, X_val, y_val, decays, hidden, epochs, lr, seed):
    """Обучить по одной сети для каждого значения weight_decay из decays (одинаковый seed).

    Вернуть список кортежей (weight_decay, train_mse, val_mse, weight_norm), округлив числа до 4 знаков (round(v, 4)).
    """
    # TODO
    return []
