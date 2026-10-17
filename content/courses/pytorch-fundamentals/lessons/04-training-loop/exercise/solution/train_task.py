import torch
from torch import nn


def train_step(model, optimizer, loss_fn, xb, yb):
    """Один шаг обучения на пакете: обнулить градиенты, прямой проход, потеря, backward, шаг оптимизатора.

    Вернуть значение потери ДО шага как обычное число float.
    """
    optimizer.zero_grad()
    loss = loss_fn(model(xb), yb)
    loss.backward()
    optimizer.step()
    return loss.item()


def evaluate(model, loss_fn, X, y):
    """Потеря на данных без обучения: без построения графа и без изменения градиентов параметров. Вернуть float."""
    with torch.no_grad():
        return loss_fn(model(X), y).item()


def train(model, optimizer, loss_fn, X, y, epochs):
    """Полнопакетное обучение: epochs раз вызвать train_step на всех данных. Вернуть список потерь (float) длины epochs."""
    return [train_step(model, optimizer, loss_fn, X, y) for _ in range(epochs)]


class _Squeeze(nn.Module):
    def __init__(self, inner):
        super().__init__()
        self.inner = inner

    def forward(self, x):
        return self.inner(x).squeeze(-1)


def fit_linear(X, y, lr, epochs):
    """Линейная регрессия на nn.Linear(d, 1): веса и смещение стартуют с нулей, SGD(lr), MSELoss.

    X формы (n, d) float32, y формы (n,). Вернуть (w, b, losses): w - тензор формы (d,) без градиента,
    b - float, losses - список потерь по эпохам.
    """
    layer = nn.Linear(X.shape[1], 1)
    with torch.no_grad():
        layer.weight.zero_()
        layer.bias.zero_()
    model = _Squeeze(layer)
    optimizer = torch.optim.SGD(model.parameters(), lr=lr)
    losses = train(model, optimizer, nn.MSELoss(), X, y, epochs)
    return layer.weight.detach().reshape(-1).clone(), layer.bias.item(), losses
