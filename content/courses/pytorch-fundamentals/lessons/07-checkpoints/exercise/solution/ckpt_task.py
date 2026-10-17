import torch
from torch import nn


def make_net():
    """Linear(4, 8) -> BatchNorm1d(8) -> ReLU -> Dropout(0.5) -> Linear(8, 2), именно в таком порядке (nn.Sequential)."""
    return nn.Sequential(nn.Linear(4, 8), nn.BatchNorm1d(8), nn.ReLU(), nn.Dropout(0.5), nn.Linear(8, 2))


def save_checkpoint(model, optimizer, epoch, path):
    """Сохранить в файл path словарь с ключами 'model' (state_dict модели), 'optimizer' (state_dict) и 'epoch'."""
    torch.save({"model": model.state_dict(), "optimizer": optimizer.state_dict(), "epoch": epoch}, path)


def load_checkpoint(model, optimizer, path):
    """Загрузить состояние из файла в model и optimizer и вернуть номер эпохи (int)."""
    state = torch.load(path)
    model.load_state_dict(state["model"])
    optimizer.load_state_dict(state["optimizer"])
    return int(state["epoch"])


def predict(model, X):
    """Предсказать классы (argmax логитов, int64, форма (n,)) в режиме eval и без градиентов.

    После вызова режим модели (train или eval) должен быть таким же, каким был до вызова.
    """
    was_training = model.training
    model.eval()
    try:
        with torch.no_grad():
            return model(X).argmax(dim=1)
    finally:
        model.train(was_training)
