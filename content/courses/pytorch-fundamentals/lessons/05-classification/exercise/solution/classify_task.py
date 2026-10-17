import torch
from torch import nn

CENTERS = [[0.0, 0.0], [3.0, 0.0], [0.0, 3.0]]


def make_blobs(n_per_class, seed):
    """Три гауссовых облака в 2D: центры CENTERS, стандартное отклонение 0.6.

    Вернуть X формы (3 * n_per_class, 2) типа float32 и метки y формы (3 * n_per_class,) типа int64:
    сначала все точки класса 0, затем класса 1, затем класса 2. Случайность - только через torch.Generator(seed).
    """
    g = torch.Generator().manual_seed(seed)
    centers = torch.tensor(CENTERS)
    noise = 0.6 * torch.randn(3, n_per_class, 2, generator=g)
    X = (centers[:, None, :] + noise).reshape(-1, 2)
    y = torch.arange(3).repeat_interleave(n_per_class)
    return X, y


def build_mlp(in_dim, hidden, n_classes):
    """Сеть Linear(in_dim, hidden) -> ReLU -> Linear(hidden, n_classes). Выход - логиты, без softmax."""
    return nn.Sequential(nn.Linear(in_dim, hidden), nn.ReLU(), nn.Linear(hidden, n_classes))


def accuracy(logits, y):
    """Доля верных предсказаний (класс с наибольшим логитом) как float."""
    return (logits.argmax(dim=1) == y).float().mean().item()


def train_classifier(X, y, hidden, epochs, lr, seed):
    """Обучить build_mlp на полном пакете: Adam(lr), CrossEntropyLoss, torch.manual_seed(seed) ДО создания сети.

    Вернуть (model, losses): сеть и список потерь по эпохам (float).
    """
    torch.manual_seed(seed)
    model = build_mlp(X.shape[1], hidden, int(y.max()) + 1)
    opt = torch.optim.Adam(model.parameters(), lr=lr)
    loss_fn = nn.CrossEntropyLoss()
    losses = []
    for _ in range(epochs):
        opt.zero_grad()
        loss = loss_fn(model(X), y)
        loss.backward()
        opt.step()
        losses.append(loss.item())
    return model, losses
