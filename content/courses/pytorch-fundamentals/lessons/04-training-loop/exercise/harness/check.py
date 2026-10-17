import json
import os

import numpy as np
import torch
from torch import nn

torch.set_num_threads(1)
NONCE = os.environ["DEVACADEMY_NONCE"]

import train_task as m


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


W_TRUE = torch.tensor([2.0, -1.0, 0.5])
B_TRUE = 1.5


def make_data(seed=0, n=100, noise=0.05):
    g = torch.Generator().manual_seed(seed)
    X = torch.randn(n, 3, generator=g)
    y = X @ W_TRUE + B_TRUE + noise * torch.randn(n, generator=g)
    return X, y


def zero_linear(d):
    layer = nn.Linear(d, 1)
    with torch.no_grad():
        layer.weight.zero_()
        layer.bias.zero_()
    return nn.Sequential(layer, nn.Flatten(0))


def t_step_decreases():
    torch.manual_seed(0)
    X, y = make_data()
    model = nn.Sequential(nn.Linear(3, 1), nn.Flatten(0))
    opt = torch.optim.SGD(model.parameters(), lr=0.05)
    loss_fn = nn.MSELoss()
    first = m.train_step(model, opt, loss_fn, X, y)
    if not isinstance(first, float):
        return f"train_step должен возвращать float, получили {type(first).__name__}"
    second = m.train_step(model, opt, loss_fn, X, y)
    if not second < first:
        return f"после шага потеря должна уменьшиться: {first:.4f} -> {second:.4f}"


def t_fit_linear():
    X, y = make_data()
    w, b, losses = m.fit_linear(X, y, 0.1, 300)
    Xb = torch.cat([torch.ones(len(X), 1), X], dim=1)
    ref = torch.linalg.lstsq(Xb, y.unsqueeze(1)).solution.squeeze(1)
    if tuple(w.shape) != (3,) or w.requires_grad or not isinstance(b, float):
        return "ожидались w формы (3,) без градиента и b типа float"
    if not torch.allclose(w, ref[1:], atol=1e-3) or abs(b - ref[0].item()) > 1e-3:
        return f"веса должны сойтись к решению наименьших квадратов {ref.tolist()}, получили {b:.4f}, {w.tolist()}"
    if len(losses) != 300 or losses[-1] > 0.01:
        return f"ожидалось 300 потерь с итоговой потерей < 0.01, получили {len(losses)} и {losses[-1]:.4f}"


def t_evaluate():
    X, y = make_data(seed=1, n=30)
    model = nn.Sequential(nn.Linear(3, 1), nn.Flatten(0))
    got = m.evaluate(model, nn.MSELoss(), X, y)
    want = ((model(X) - y) ** 2).mean().item()
    if not isinstance(got, float) or abs(got - want) > 1e-5:
        return f"evaluate должен вернуть float {want:.5f}, получили {got!r}"
    if any(p.grad is not None for p in model.parameters()):
        return "evaluate не должен создавать градиенты: вычисляйте под torch.no_grad() и не вызывайте backward"


def t_matches_manual_gd():
    X, y = make_data(seed=2, n=50)
    model = zero_linear(3)
    opt = torch.optim.SGD(model.parameters(), lr=0.05)
    losses = m.train(model, opt, nn.MSELoss(), X, y, 15)
    Xb = np.hstack([np.ones((50, 1)), X.numpy().astype(np.float64)])
    yn = y.numpy().astype(np.float64)
    w = np.zeros(4)
    want = []
    for _ in range(15):
        r = Xb @ w - yn
        want.append(float(np.mean(r ** 2)))
        w = w - 0.05 * (2.0 / 50) * Xb.T @ r
    if len(losses) != 15 or not np.allclose(losses, want, rtol=1e-4, atol=1e-5):
        return "потери должны совпасть с ручным градиентным спуском; проверьте optimizer.zero_grad() перед backward в каждом шаге"
    got_w = model[0].weight.detach().numpy().reshape(-1)
    if not np.allclose(got_w, w[1:], atol=1e-4):
        return "итоговые веса должны совпасть с ручным градиентным спуском"


def t_zero_grad_needed():
    import copy

    X, y = make_data(seed=3, n=40)
    model = zero_linear(3)
    opt = torch.optim.SGD(model.parameters(), lr=0.1)
    loss_fn = nn.MSELoss()
    m.train_step(model, opt, loss_fn, X, y)
    before = copy.deepcopy(model)
    m.train_step(model, opt, loss_fn, X, y)
    got = model[0].weight.grad
    if got is None:
        return "после train_step в .grad должен лежать градиент последнего шага (не обнуляйте градиенты после backward)"
    loss_fn(before(X), y).backward()
    fresh = before[0].weight.grad
    if not torch.allclose(got, fresh, atol=1e-6):
        return "градиент второго шага должен быть свежим, а не суммой с предыдущим: вызовите optimizer.zero_grad() в начале каждого шага"


def t_adam():
    torch.manual_seed(0)
    X, y = make_data(seed=4)
    model = nn.Sequential(nn.Linear(3, 8), nn.ReLU(), nn.Linear(8, 1), nn.Flatten(0))
    opt = torch.optim.Adam(model.parameters(), lr=0.05)
    losses = m.train(model, opt, nn.MSELoss(), X, y, 200)
    if not isinstance(losses[0], float):
        return "train должен возвращать список float"
    if losses[-1] > 0.05 * losses[0]:
        return f"Adam за 200 эпох должен уменьшить потерю минимум в 20 раз: {losses[0]:.3f} -> {losses[-1]:.3f}"


def t_edge_epochs():
    X, y = make_data(seed=5, n=10)
    model = zero_linear(3)
    opt = torch.optim.SGD(model.parameters(), lr=0.1)
    if m.train(model, opt, nn.MSELoss(), X, y, 0) != []:
        return "при epochs = 0 ожидался пустой список и неизменная модель"
    if model[0].weight.abs().sum().item() != 0.0:
        return "при epochs = 0 веса не должны меняться"
    w, b, losses = m.fit_linear(X, y, 0.1, 0)
    if losses != [] or b != 0.0 or w.abs().sum().item() != 0.0:
        return "fit_linear при epochs = 0 должен вернуть нулевые веса, b = 0.0 и пустой список потерь"


case("step-decreases", t_step_decreases)
case("fit-linear", t_fit_linear)
case("evaluate", t_evaluate)
case("matches-manual-gd", t_matches_manual_gd)
case("zero-grad-needed", t_zero_grad_needed)
case("adam", t_adam)
case("edge-epochs", t_edge_epochs)
