import torch
from torch import nn


def make_net():
    """Linear(4, 8) -> BatchNorm1d(8) -> ReLU -> Dropout(0.5) -> Linear(8, 2), именно в таком порядке (nn.Sequential)."""
    # TODO
    return nn.Identity()


def save_checkpoint(model, optimizer, epoch, path):
    """Сохранить в файл path словарь с ключами 'model' (state_dict модели), 'optimizer' (state_dict) и 'epoch'."""
    # TODO
    pass


def load_checkpoint(model, optimizer, path):
    """Загрузить состояние из файла в model и optimizer и вернуть номер эпохи (int)."""
    # TODO
    return -1


def predict(model, X):
    """Предсказать классы (argmax логитов, int64, форма (n,)) в режиме eval и без градиентов.

    После вызова режим модели (train или eval) должен быть таким же, каким был до вызова.
    """
    # TODO
    return torch.zeros(len(X), dtype=torch.long)
