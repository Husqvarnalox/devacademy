import copy

import torch
from torch import nn
from torch.utils.data import DataLoader, Dataset


class PointsDataset(Dataset):
    """Набор пар (x_i, y_i): __len__ и __getitem__(i) -> (X[i], y[i])."""

    def __init__(self, X, y):
        # TODO: сохранить X и y; при разной длине выбросить ValueError
        pass

    def __len__(self):
        # TODO
        return 0

    def __getitem__(self, i):
        # TODO
        return None


def split_indices(n, val_fraction, seed):
    """Случайно разбить индексы 0..n-1 на (train_idx, val_idx) - тензоры int64.

    Размер валидации - round(n * val_fraction); перемешивание через torch.randperm(n, generator=...) с seed.
    """
    # TODO
    return torch.arange(n), torch.zeros(0, dtype=torch.long)


def make_loader(dataset, batch_size, shuffle, seed):
    """DataLoader без рабочих процессов; порядок перемешивания задаётся генератором с seed."""
    # TODO
    return None


def fit_early_stopping(model, train_loader, X_val, y_val, lr, max_epochs, patience):
    """Обучение Adam(lr) с CrossEntropyLoss и ранней остановкой по потере на валидации.

    Каждую эпоху: пройти по train_loader, затем посчитать потерю на (X_val, y_val) без градиентов.
    Лучшей считается эпоха со строго меньшей потерей. Остановиться, когда patience эпох подряд не было улучшения,
    либо после max_epochs эпох. В конце вернуть в модель веса лучшей эпохи.
    Вернуть (best_epoch, val_losses): индекс лучшей эпохи (с нуля) и список потерь на валидации по всем выполненным эпохам.
    """
    # TODO
    return -1, []
