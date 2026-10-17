import copy

import torch
from torch import nn
from torch.utils.data import DataLoader, Dataset


class PointsDataset(Dataset):
    """Набор пар (x_i, y_i): __len__ и __getitem__(i) -> (X[i], y[i])."""

    def __init__(self, X, y):
        if len(X) != len(y):
            raise ValueError("X и y должны иметь одинаковую длину")
        self.X = X
        self.y = y

    def __len__(self):
        return len(self.X)

    def __getitem__(self, i):
        return self.X[i], self.y[i]


def split_indices(n, val_fraction, seed):
    """Случайно разбить индексы 0..n-1 на (train_idx, val_idx) - тензоры int64.

    Размер валидации - round(n * val_fraction); перемешивание через torch.randperm(n, generator=...) с seed.
    """
    g = torch.Generator().manual_seed(seed)
    perm = torch.randperm(n, generator=g)
    n_val = round(n * val_fraction)
    return perm[n_val:], perm[:n_val]


def make_loader(dataset, batch_size, shuffle, seed):
    """DataLoader без рабочих процессов; порядок перемешивания задаётся генератором с seed."""
    g = torch.Generator().manual_seed(seed)
    return DataLoader(dataset, batch_size=batch_size, shuffle=shuffle, generator=g, num_workers=0)


def fit_early_stopping(model, train_loader, X_val, y_val, lr, max_epochs, patience):
    """Обучение Adam(lr) с CrossEntropyLoss и ранней остановкой по потере на валидации.

    Каждую эпоху: пройти по train_loader, затем посчитать потерю на (X_val, y_val) без градиентов.
    Лучшей считается эпоха со строго меньшей потерей. Остановиться, когда patience эпох подряд не было улучшения,
    либо после max_epochs эпох. В конце вернуть в модель веса лучшей эпохи.
    Вернуть (best_epoch, val_losses): индекс лучшей эпохи (с нуля) и список потерь на валидации по всем выполненным эпохам.
    """
    opt = torch.optim.Adam(model.parameters(), lr=lr)
    loss_fn = nn.CrossEntropyLoss()
    val_losses = []
    best = float("inf")
    best_epoch = -1
    best_state = None
    for epoch in range(max_epochs):
        model.train()
        for xb, yb in train_loader:
            opt.zero_grad()
            loss_fn(model(xb), yb).backward()
            opt.step()
        model.eval()
        with torch.no_grad():
            v = loss_fn(model(X_val), y_val).item()
        val_losses.append(v)
        if v < best:
            best, best_epoch = v, epoch
            best_state = copy.deepcopy(model.state_dict())
        elif epoch - best_epoch >= patience:
            break
    if best_state is not None:
        model.load_state_dict(best_state)
    return best_epoch, val_losses
