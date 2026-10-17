import torch
from torch import nn

CENTERS = [[0.0, 0.0], [3.0, 0.0], [0.0, 3.0]]


def make_blobs(n_per_class, seed):
    """Три гауссовых облака в 2D: центры CENTERS, стандартное отклонение 0.6.

    Вернуть X формы (3 * n_per_class, 2) типа float32 и метки y формы (3 * n_per_class,) типа int64:
    сначала все точки класса 0, затем класса 1, затем класса 2. Случайность - только через torch.Generator(seed).
    """
    # TODO
    return torch.zeros(0, 2), torch.zeros(0, dtype=torch.long)


def build_mlp(in_dim, hidden, n_classes):
    """Сеть Linear(in_dim, hidden) -> ReLU -> Linear(hidden, n_classes). Выход - логиты, без softmax."""
    # TODO
    return nn.Identity()


def accuracy(logits, y):
    """Доля верных предсказаний (класс с наибольшим логитом) как float."""
    # TODO
    return 0.0


def train_classifier(X, y, hidden, epochs, lr, seed):
    """Обучить build_mlp на полном пакете: Adam(lr), CrossEntropyLoss, torch.manual_seed(seed) ДО создания сети.

    Вернуть (model, losses): сеть и список потерь по эпохам (float).
    """
    # TODO
    return nn.Identity(), []
