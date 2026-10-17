import json
import os

import torch
from torch import nn

torch.set_num_threads(1)
NONCE = os.environ["DEVACADEMY_NONCE"]

import data_task as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() возвращает None при успехе или строку с причиной провала; исключения - провал."""
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def noisy_data(seed=0, n=90):
    g = torch.Generator().manual_seed(seed)
    X = torch.randn(n, 2, generator=g)
    y = ((X[:, 0] + 1.5 * torch.randn(n, generator=g)) > 0).long()
    return X, y


def make_net(seed=0):
    torch.manual_seed(seed)
    return nn.Sequential(nn.Linear(2, 64), nn.ReLU(), nn.Linear(64, 64), nn.ReLU(), nn.Linear(64, 2))


def t_dataset():
    X = torch.arange(10, dtype=torch.float32).reshape(5, 2)
    y = torch.tensor([0, 1, 0, 1, 1])
    ds = m.PointsDataset(X, y)
    if len(ds) != 5:
        return f"len(ds) должен быть 5, получили {len(ds)}"
    x2, y2 = ds[2]
    if not torch.equal(x2, X[2]) or int(y2) != 0:
        return "ds[i] должен возвращать пару (X[i], y[i])"
    try:
        m.PointsDataset(X, y[:3])
    except ValueError:
        pass
    else:
        return "при разной длине X и y ожидалось ValueError"


def t_split():
    tr, va = m.split_indices(20, 0.25, 0)
    if len(va) != 5 or len(tr) != 15:
        return f"ожидалось 15 + 5 индексов, получили {len(tr)} + {len(va)}"
    if tr.dtype != torch.int64 or va.dtype != torch.int64:
        return "индексы должны иметь тип int64"
    if sorted(tr.tolist() + va.tolist()) != list(range(20)):
        return "train и val не должны пересекаться и вместе должны покрывать все индексы"


def t_loader_batches():
    X, y = noisy_data(n=10)
    loader = m.make_loader(m.PointsDataset(X, y), 4, False, 0)
    shapes = [tuple(xb.shape) for xb, _ in loader]
    if shapes != [(4, 2), (4, 2), (2, 2)]:
        return f"10 объектов по 4 в пакете должны дать формы (4,2), (4,2), (2,2); получили {shapes}"
    got = torch.cat([xb for xb, _ in loader])
    if not torch.equal(got, X):
        return "при shuffle=False порядок объектов должен сохраняться"


def t_loader_shuffle():
    X, y = noisy_data(n=40)
    ds = m.PointsDataset(X, y)
    a = torch.cat([xb for xb, _ in m.make_loader(ds, 8, True, 3)])
    b = torch.cat([xb for xb, _ in m.make_loader(ds, 8, True, 3)])
    c = torch.cat([xb for xb, _ in m.make_loader(ds, 8, True, 4)])
    if not torch.equal(a, b):
        return "одинаковый seed должен давать одинаковый порядок"
    if torch.equal(a, c) or torch.equal(a, X):
        return "shuffle=True должен перемешивать, а разный seed - давать разный порядок"
    if sorted(a[:, 0].tolist()) != sorted(X[:, 0].tolist()):
        return "перемешивание не должно терять объекты"


def t_split_deterministic():
    a = m.split_indices(50, 0.2, 7)
    b = m.split_indices(50, 0.2, 7)
    c = m.split_indices(50, 0.2, 8)
    if not (torch.equal(a[0], b[0]) and torch.equal(a[1], b[1])):
        return "одинаковый seed должен давать одинаковое разбиение"
    if torch.equal(a[1], c[1]):
        return "разный seed должен давать разное разбиение"
    tr, va = m.split_indices(7, 0.5, 0)
    if len(va) != round(3.5) or len(tr) + len(va) != 7:
        return "размер валидации - round(n * val_fraction)"


def run_early(patience=8, max_epochs=300, seed=0):
    X, y = noisy_data()
    tr, va = m.split_indices(len(X), 0.4, 1)
    loader = m.make_loader(m.PointsDataset(X[tr], y[tr]), 18, True, seed)
    model = make_net(seed)
    best, losses = m.fit_early_stopping(model, loader, X[va], y[va], 0.02, max_epochs, patience)
    return model, best, losses, X[va], y[va]


def t_early_stops():
    model, best, losses, Xv, yv = run_early()
    if not losses or not all(isinstance(v, float) for v in losses):
        return "val_losses должен быть непустым списком float"
    if losses[best] != min(losses):
        return f"best_epoch должен указывать на минимум val_losses: {best}, минимум в эпохе {losses.index(min(losses))}"
    if len(losses) != best + 1 + 8:
        return f"остановка должна произойти через patience = 8 эпох после лучшей: best_epoch = {best}, эпох выполнено {len(losses)}"
    if len(losses) >= 300:
        return "на зашумлённых данных ожидалась ранняя остановка"


def t_restores_best():
    model, best, losses, Xv, yv = run_early()
    model.eval()
    with torch.no_grad():
        v = nn.CrossEntropyLoss()(model(Xv), yv).item()
    if abs(v - losses[best]) > 1e-6:
        return f"в модель должны вернуться веса лучшей эпохи: потеря {v:.5f}, ожидалась {losses[best]:.5f}"


def t_max_epochs_and_patience():
    X, y = noisy_data(seed=2, n=30)
    loader = m.make_loader(m.PointsDataset(X, y), 10, True, 0)
    model = make_net(1)
    best, losses = m.fit_early_stopping(model, loader, X[:10], y[:10], 0.01, 3, 100)
    if len(losses) != 3:
        return f"при max_epochs = 3 и большом patience ожидалось 3 эпохи, получили {len(losses)}"
    model2 = make_net(1)
    best2, losses2 = m.fit_early_stopping(model2, loader, X[:10], y[:10], 0.01, 0, 5)
    if losses2 != [] or best2 != -1:
        return "при max_epochs = 0 ожидалось (-1, [])"


def t_matches_reference():
    import copy

    X, y = noisy_data(seed=5, n=60)
    tr, va = m.split_indices(60, 0.3, 2)
    ds = m.PointsDataset(X[tr], y[tr])
    got_model = make_net(3)
    best, losses = m.fit_early_stopping(got_model, m.make_loader(ds, 14, True, 9), X[va], y[va], 0.02, 40, 5)
    ref = make_net(3)
    opt = torch.optim.Adam(ref.parameters(), lr=0.02)
    lf = nn.CrossEntropyLoss()
    loader = m.make_loader(ds, 14, True, 9)
    want, bv, bi = [], float("inf"), -1
    for e in range(40):
        for xb, yb in loader:
            opt.zero_grad()
            lf(ref(xb), yb).backward()
            opt.step()
        with torch.no_grad():
            v = lf(ref(X[va]), y[va]).item()
        want.append(v)
        if v < bv:
            bv, bi = v, e
        elif e - bi >= 5:
            break
    if best != bi or len(losses) != len(want) or any(abs(a - b) > 1e-5 for a, b in zip(losses, want)):
        return "обучение должно совпасть с эталонным циклом: Adam, CrossEntropyLoss, один проход по загрузчику за эпоху, остановка при epoch - best_epoch >= patience"


case("dataset", t_dataset)
case("split", t_split)
case("loader-batches", t_loader_batches)
case("loader-shuffle", t_loader_shuffle)
case("split-deterministic", t_split_deterministic)
case("early-stops", t_early_stops)
case("restores-best", t_restores_best)
case("max-epochs-patience", t_max_epochs_and_patience)
case("matches-reference", t_matches_reference)
