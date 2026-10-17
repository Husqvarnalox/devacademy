import json
import os

import torch
from torch import nn

torch.set_num_threads(1)
NONCE = os.environ["DEVACADEMY_NONCE"]

import wd_task as m


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


def data(seed, n):
    g = torch.Generator().manual_seed(seed)
    X = torch.randn(n, 5, generator=g)
    y = X[:, 0] - 0.5 * X[:, 1] + 0.3 * torch.randn(n, generator=g)
    return X, y


Xtr, ytr = data(0, 20)
Xva, yva = data(1, 200)


def t_weight_norm():
    net = nn.Sequential(nn.Linear(2, 2), nn.Linear(2, 1))
    with torch.no_grad():
        net[0].weight.copy_(torch.tensor([[3.0, 0.0], [0.0, 4.0]]))
        net[0].bias.fill_(100.0)
        net[1].weight.copy_(torch.tensor([[12.0, 0.0]]))
        net[1].bias.fill_(100.0)
    got = m.weight_norm(net)
    if not isinstance(got, float) or abs(got - 13.0) > 1e-5:
        return f"норма весов без смещений: sqrt(9 + 16 + 144) = 13, получили {got!r}"


def t_mse():
    net = nn.Linear(1, 1)
    with torch.no_grad():
        net.weight.fill_(2.0)
        net.bias.zero_()
    X = torch.tensor([[1.0], [2.0]])
    y = torch.tensor([1.0, 4.0])
    got = m.mse(net, X, y)
    if not isinstance(got, float) or abs(got - 0.5) > 1e-6:
        return f"ожидалось 0.5, получили {got!r}"


def t_train_shapes():
    model = m.train_regularized(Xtr, ytr, 16, 0.0, 5, 0.02, 0)
    if tuple(model(Xtr).shape) not in ((20, 1), (20,)):
        return "модель должна вернуть предсказания для 20 объектов"
    if sum(p.numel() for p in model.parameters()) != 5 * 16 + 16 + 16 + 1:
        return "ожидалась сеть Linear(5, 16) -> ReLU -> Linear(16, 1)"


def t_decay_shrinks_norm():
    rows = m.compare_weight_decay(Xtr, ytr, Xva, yva, [0.0, 0.03, 0.3], 64, 300, 0.02, 0)
    if [r[0] for r in rows] != [0.0, 0.03, 0.3] or any(len(r) != 4 for r in rows):
        return "ожидался список кортежей (weight_decay, train_mse, val_mse, weight_norm) в порядке decays"
    norms = [r[3] for r in rows]
    if not (norms[0] > norms[1] > norms[2]):
        return f"норма весов должна убывать с ростом weight_decay: {norms}"


def t_decay_raises_train_loss():
    rows = m.compare_weight_decay(Xtr, ytr, Xva, yva, [0.0, 0.03], 64, 300, 0.02, 0)
    if len(rows) != 2:
        return "ожидалось две строки результата"
    if not rows[1][1] > rows[0][1]:
        return f"регуляризация должна ухудшать ошибку на обучении: {rows[0][1]} -> {rows[1][1]}"
    if not rows[1][2] < rows[0][2]:
        return f"на этих данных weight_decay = 0.03 должен улучшить ошибку на валидации: {rows[0][2]} -> {rows[1][2]}"


def t_matches_manual():
    wd, lr, epochs = 0.3, 0.05, 25
    got = m.train_regularized(Xtr, ytr, 8, wd, epochs, lr, 3)
    torch.manual_seed(3)
    ref = nn.Sequential(nn.Linear(5, 8), nn.ReLU(), nn.Linear(8, 1))
    for _ in range(epochs):
        ref.zero_grad()
        nn.functional.mse_loss(ref(Xtr).squeeze(-1), ytr).backward()
        with torch.no_grad():
            for p in ref.parameters():
                p -= lr * (p.grad + wd * p)
    if len(list(got.parameters())) != 4:
        return "ожидалась сеть с четырьмя параметрами (два слоя Linear)"
    for a, b in zip(got.parameters(), ref.parameters()):
        if not torch.allclose(a, b, atol=1e-5):
            return "weight_decay в SGD должен совпадать с ручным обновлением p -= lr * (grad + wd * p) для всех параметров"


def t_rounding_and_seed():
    a = m.compare_weight_decay(Xtr, ytr, Xva, yva, [0.0, 0.5], 16, 50, 0.02, 4)
    b = m.compare_weight_decay(Xtr, ytr, Xva, yva, [0.0, 0.5], 16, 50, 0.02, 4)
    if len(a) != 2 or a != b:
        return "при одинаковом seed результаты должны совпадать"
    if any(round(v, 4) != v for r in a for v in r[1:]):
        return "числа должны быть округлены до 4 знаков"
    if m.compare_weight_decay(Xtr, ytr, Xva, yva, [], 16, 50, 0.02, 4) != []:
        return "для пустого decays ожидался пустой список"


def t_zero_decay_is_plain_sgd():
    got = m.train_regularized(Xtr, ytr, 8, 0.0, 10, 0.05, 6)
    torch.manual_seed(6)
    ref = nn.Sequential(nn.Linear(5, 8), nn.ReLU(), nn.Linear(8, 1))
    opt = torch.optim.SGD(ref.parameters(), lr=0.05)
    for _ in range(10):
        opt.zero_grad()
        nn.functional.mse_loss(ref(Xtr).squeeze(-1), ytr).backward()
        opt.step()
    if len(list(got.parameters())) != 4:
        return "ожидалась сеть с четырьмя параметрами (два слоя Linear)"
    for a, b in zip(got.parameters(), ref.parameters()):
        if not torch.allclose(a, b, atol=1e-6):
            return "при weight_decay = 0 обучение должно совпасть с обычным SGD"


case("weight-norm", t_weight_norm)
case("mse", t_mse)
case("train-shapes", t_train_shapes)
case("decay-shrinks-norm", t_decay_shrinks_norm)
case("decay-raises-train-loss", t_decay_raises_train_loss)
case("matches-manual", t_matches_manual)
case("rounding-and-seed", t_rounding_and_seed)
case("zero-decay-is-plain-sgd", t_zero_decay_is_plain_sgd)
