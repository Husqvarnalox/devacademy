import json
import os

import torch

torch.set_num_threads(1)
NONCE = os.environ["DEVACADEMY_NONCE"]

import autograd_task as m


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


def data(seed=0, n=20, d=3):
    g = torch.Generator().manual_seed(seed)
    X = torch.randn(n, d, generator=g, dtype=torch.float64)
    y = torch.randn(n, generator=g, dtype=torch.float64)
    w = torch.randn(d, generator=g, dtype=torch.float64)
    return X, y, w


def t_poly_grad():
    for x0 in (2.0, 0.0, -1.5):
        got = m.poly_grad(x0)
        want = 9 * x0 ** 2 - 2
        if not isinstance(got, float) or abs(got - want) > 1e-4:
            return f"poly_grad({x0}) должна быть float {want}, получили {got!r}"


def t_mse_grad():
    X, y, w = data()
    g = m.mse_grad(X, y, w)
    want = 2.0 / len(X) * X.T @ (X @ w - y)
    if tuple(g.shape) != tuple(w.shape) or not torch.allclose(g, want, atol=1e-9):
        return f"градиент должен совпасть с (2/n) Xᵀ (Xw - y): ожидали {want.tolist()}, получили {g.tolist()}"


def t_accumulated_grad():
    got = m.accumulated_grad(3.0, 4, False)
    if abs(got - 24.0) > 1e-9:
        return f"без обнуления 4 backward для x ** 2 в точке 3 дают 4 * 6 = 24, получили {got!r}"
    got = m.accumulated_grad(3.0, 4, True)
    if abs(got - 6.0) > 1e-9:
        return f"с обнулением перед каждым backward градиент равен 2x = 6, получили {got!r}"


def t_no_grad():
    w = torch.tensor([1.0, 2.0], requires_grad=True)
    X = torch.tensor([[1.0, 1.0], [2.0, 0.5]])
    out = m.eval_without_grad(w, X)
    if out.requires_grad or out.grad_fn is not None:
        return "результат не должен участвовать в графе: используйте torch.no_grad()"
    if not torch.allclose(out, torch.tensor([3.0, 3.0])):
        return f"ожидалось [3.0, 3.0], получили {out.tolist()}"
    if not w.requires_grad:
        return "сам параметр w должен остаться с requires_grad=True"


def t_grad_check():
    x = torch.tensor([0.5, -1.0, 2.0], dtype=torch.float64)
    before = x.clone()
    err = m.max_grad_error(lambda v: (v ** 3).sum() + (v[0] * v[1]) + torch.sin(v).sum(), x)
    if not isinstance(err, float) or err > 1e-6:
        return f"autograd и конечные разности должны совпасть (ошибка < 1e-6), получили {err!r}"
    if not torch.equal(x, before) or x.requires_grad:
        return "входной x менять нельзя"
    bad = m.max_grad_error(lambda v: (v ** 2).sum(), torch.tensor([1.0, 2.0], dtype=torch.float64))
    if bad > 1e-6:
        return f"для сумм квадратов ошибка тоже должна быть мала, получили {bad!r}"


def t_mse_grad_clean():
    X, y, w = data(seed=1, n=8, d=2)
    w_before = w.clone()
    g = m.mse_grad(X, y, w)
    if w.requires_grad or w.grad is not None or not torch.equal(w, w_before):
        return "w нельзя менять: не включайте ему requires_grad и не пишите в w.grad, работайте с копией"
    if g.requires_grad:
        return "результат должен быть обычным тензором без истории: возьмите p.grad после backward"
    X2, y2, w2 = data(seed=2, n=5, d=4)
    g2 = m.mse_grad(X2, y2, w2)
    if not torch.allclose(g2, 2.0 / 5 * X2.T @ (X2 @ w2 - y2), atol=1e-9):
        return "для других данных градиент тоже должен совпасть с аналитическим"


def t_accumulate_edge():
    if m.accumulated_grad(5.0, 0, False) != 0.0 or m.accumulated_grad(5.0, 0, True) != 0.0:
        return "при k = 0 backward не вызывается, ожидалось 0.0"
    if abs(m.accumulated_grad(-2.0, 3, False) + 12.0) > 1e-9:
        return "для x0 = -2 и k = 3 без обнуления ожидалось -12.0"


case("poly-grad", t_poly_grad)
case("mse-grad", t_mse_grad)
case("accumulated-grad", t_accumulated_grad)
case("no-grad", t_no_grad)
case("grad-check", t_grad_check)
case("mse-grad-clean", t_mse_grad_clean)
case("accumulate-edge", t_accumulate_edge)
