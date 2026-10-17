import json
import os

import torch
from torch import nn

torch.set_num_threads(1)
NONCE = os.environ["DEVACADEMY_NONCE"]

import classify_task as m


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


def t_blobs_shape():
    X, y = m.make_blobs(20, 0)
    if tuple(X.shape) != (60, 2) or X.dtype != torch.float32:
        return f"X: ожидалась форма (60, 2) и float32, получили {tuple(X.shape)} {X.dtype}"
    if tuple(y.shape) != (60,) or y.dtype != torch.int64:
        return f"y: ожидалась форма (60,) и int64, получили {tuple(y.shape)} {y.dtype}"
    if y.tolist() != [0] * 20 + [1] * 20 + [2] * 20:
        return "метки должны идти блоками: 20 нулей, 20 единиц, 20 двоек"
    means = [X[y == k].mean(dim=0) for k in range(3)]
    for k, c in enumerate(m.CENTERS):
        if (means[k] - torch.tensor(c)).abs().max() > 0.6:
            return f"среднее класса {k} должно быть около центра {c}, получили {means[k].tolist()}"


def t_mlp_shape():
    torch.manual_seed(0)
    net = m.build_mlp(2, 8, 3)
    out = net(torch.randn(5, 2))
    if tuple(out.shape) != (5, 3):
        return f"ожидалась форма логитов (5, 3), получили {tuple(out.shape)}"
    layers = list(net.children())
    if len(layers) != 3 or not isinstance(layers[0], nn.Linear) or not isinstance(layers[1], nn.ReLU) or not isinstance(layers[2], nn.Linear):
        return "ожидалась цепочка Linear -> ReLU -> Linear"
    if sum(p.numel() for p in net.parameters()) != 2 * 8 + 8 + 8 * 3 + 3:
        return "неверное число параметров: проверьте размеры слоёв"


def t_accuracy():
    logits = torch.tensor([[2.0, 0.1, 0.0], [0.0, 1.0, 3.0], [5.0, 4.0, 0.0], [0.1, 0.2, 0.9]])
    y = torch.tensor([0, 2, 1, 2])
    got = m.accuracy(logits, y)
    if not isinstance(got, float) or abs(got - 0.75) > 1e-9:
        return f"ожидалось 0.75 (float), получили {got!r}"


def t_train_accuracy():
    X, y = m.make_blobs(60, 1)
    model, losses = m.train_classifier(X, y, 16, 150, 0.05, 0)
    acc = m.accuracy(model(X), y)
    if acc < 0.95:
        return f"точность на обучающих данных должна быть не ниже 0.95, получили {acc:.3f}"
    if len(losses) != 150 or not isinstance(losses[0], float) or losses[-1] > 0.5 * losses[0]:
        return f"ожидалось 150 потерь float с падением минимум вдвое: {losses[0]:.3f} -> {losses[-1]:.3f}"


def t_generalizes():
    Xtr, ytr = m.make_blobs(60, 2)
    Xte, yte = m.make_blobs(60, 3)
    model, _ = m.train_classifier(Xtr, ytr, 16, 150, 0.05, 0)
    with torch.no_grad():
        acc = m.accuracy(model(Xte), yte)
    if acc < 0.9:
        return f"точность на новых данных должна быть не ниже 0.9, получили {acc:.3f}; проверьте make_blobs и метки классов"


def t_deterministic():
    X, y = m.make_blobs(30, 4)
    X2, y2 = m.make_blobs(30, 4)
    if not torch.equal(X, X2) or not torch.equal(y, y2):
        return "одинаковый seed должен давать одинаковые данные"
    X3, _ = m.make_blobs(30, 5)
    if torch.equal(X, X3):
        return "разные seed должны давать разные данные"
    _, l1 = m.train_classifier(X, y, 8, 20, 0.05, 7)
    _, l2 = m.train_classifier(X, y, 8, 20, 0.05, 7)
    if l1 != l2:
        return "torch.manual_seed(seed) должен вызываться до создания сети: потери должны совпадать при одинаковом seed"


def t_logits_not_softmax():
    X, y = m.make_blobs(30, 6)
    model, _ = m.train_classifier(X, y, 8, 60, 0.05, 0)
    if any(isinstance(mod, (nn.Softmax, nn.LogSoftmax)) for mod in model.modules()):
        return "CrossEntropyLoss сама применяет log-softmax: сеть должна возвращать логиты, без Softmax"
    out = model(X)
    if torch.allclose(out.sum(dim=1), torch.ones(len(X)), atol=1e-3):
        return "выход сети - логиты, а не вероятности"


def t_edge():
    X, y = m.make_blobs(5, 8)
    model, losses = m.train_classifier(X, y, 4, 0, 0.1, 0)
    if losses != []:
        return "при epochs = 0 список потерь должен быть пустым"
    if tuple(model(X).shape) != (15, 3):
        return "модель должна быть создана даже при epochs = 0"
    X1, y1 = m.make_blobs(1, 0)
    if tuple(X1.shape) != (3, 2) or y1.tolist() != [0, 1, 2]:
        return "для n_per_class = 1 ожидались 3 точки с метками [0, 1, 2]"


case("blobs-shape", t_blobs_shape)
case("mlp-shape", t_mlp_shape)
case("accuracy", t_accuracy)
case("train-accuracy", t_train_accuracy)
case("generalizes", t_generalizes)
case("deterministic", t_deterministic)
case("logits-not-softmax", t_logits_not_softmax)
case("edge", t_edge)
