import torch


def poly_grad(x0):
    """Производная f(x) = 3x^3 - 2x + 1 в точке x0, посчитанная через autograd. Вернуть float."""
    x = torch.tensor(float(x0), requires_grad=True)
    f = 3 * x ** 3 - 2 * x + 1
    f.backward()
    return x.grad.item()


def mse_grad(X, y, w):
    """Градиент mean((X @ w - y) ** 2) по w, посчитанный через autograd.

    X формы (n, d), y формы (n,), w формы (d,) - обычный тензор без градиентов; его нельзя менять.
    Вернуть тензор формы (d,).
    """
    p = w.detach().clone().requires_grad_(True)
    loss = ((X @ p - y) ** 2).mean()
    loss.backward()
    return p.grad


def accumulated_grad(x0, k, zero_each_time):
    """x = тензор(x0, float64) с requires_grad. k раз: loss = x ** 2, loss.backward().

    Если zero_each_time, перед каждым backward обнуляйте x.grad. Вернуть x.grad.item() (при k = 0 - 0.0).
    """
    x = torch.tensor(float(x0), dtype=torch.float64, requires_grad=True)
    for _ in range(k):
        if zero_each_time:
            x.grad = None
        (x ** 2).backward()
    return 0.0 if x.grad is None else x.grad.item()


def eval_without_grad(w, X):
    """w - параметр (requires_grad=True), X - данные. Вернуть X @ w, вычисленное без построения графа."""
    with torch.no_grad():
        return X @ w


def max_grad_error(f, x):
    """f: тензор float64 -> скаляр-тензор; x - тензор float64 формы (d,).

    Вернуть max |autograd-градиент - центральная разность| по всем координатам; шаг eps = 1e-6. x не менять. Вернуть float.
    """
    p = x.detach().clone().requires_grad_(True)
    f(p).backward()
    eps = 1e-6
    worst = 0.0
    with torch.no_grad():
        for i in range(x.numel()):
            hi = x.detach().clone()
            lo = x.detach().clone()
            hi[i] += eps
            lo[i] -= eps
            numeric = (f(hi) - f(lo)).item() / (2 * eps)
            worst = max(worst, abs(p.grad[i].item() - numeric))
    return worst
