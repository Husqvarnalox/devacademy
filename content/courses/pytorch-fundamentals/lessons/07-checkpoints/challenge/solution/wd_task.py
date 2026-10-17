import torch
from torch import nn


def train_regularized(X, y, hidden, weight_decay, epochs, lr, seed):
    """Регрессия сетью Linear(d, hidden) -> ReLU -> Linear(hidden, 1) на полном пакете.

    torch.manual_seed(seed) ДО создания сети; SGD(lr, weight_decay=weight_decay), потеря MSELoss;
    выход сети приводится к форме (n,). Вернуть обученную модель.
    """
    torch.manual_seed(seed)
    model = nn.Sequential(nn.Linear(X.shape[1], hidden), nn.ReLU(), nn.Linear(hidden, 1))
    opt = torch.optim.SGD(model.parameters(), lr=lr, weight_decay=weight_decay)
    loss_fn = nn.MSELoss()
    for _ in range(epochs):
        opt.zero_grad()
        loss_fn(model(X).squeeze(-1), y).backward()
        opt.step()
    return model


def weight_norm(model):
    """L2-норма всех параметров-матриц весов (тензоры с dim() >= 2, смещения не входят) как float."""
    total = 0.0
    for p in model.parameters():
        if p.dim() >= 2:
            total += float((p.detach() ** 2).sum())
    return total ** 0.5


def mse(model, X, y):
    """Среднеквадратичная ошибка модели на (X, y) без градиентов, float."""
    with torch.no_grad():
        return float(((model(X).squeeze(-1) - y) ** 2).mean())


def compare_weight_decay(X, y, X_val, y_val, decays, hidden, epochs, lr, seed):
    """Обучить по одной сети для каждого значения weight_decay из decays (одинаковый seed).

    Вернуть список кортежей (weight_decay, train_mse, val_mse, weight_norm), округлив числа до 4 знаков (round(v, 4)).
    """
    rows = []
    for wd in decays:
        model = train_regularized(X, y, hidden, wd, epochs, lr, seed)
        rows.append((wd, round(mse(model, X, y), 4), round(mse(model, X_val, y_val), 4), round(weight_norm(model), 4)))
    return rows
