import torch


def poly_grad(x0):
    """Производная f(x) = 3x^3 - 2x + 1 в точке x0, посчитанная через autograd. Вернуть float."""
    # TODO
    return 0.0


def mse_grad(X, y, w):
    """Градиент mean((X @ w - y) ** 2) по w, посчитанный через autograd.

    X формы (n, d), y формы (n,), w формы (d,) - обычный тензор без градиентов; его нельзя менять.
    Вернуть тензор формы (d,).
    """
    # TODO
    return torch.zeros_like(w)


def accumulated_grad(x0, k, zero_each_time):
    """x = тензор(x0, float64) с requires_grad. k раз: loss = x ** 2, loss.backward().

    Если zero_each_time, перед каждым backward обнуляйте x.grad. Вернуть x.grad.item() (при k = 0 - 0.0).
    """
    # TODO
    return 0.0


def eval_without_grad(w, X):
    """w - параметр (requires_grad=True), X - данные. Вернуть X @ w, вычисленное без построения графа."""
    # TODO
    return X @ w


def max_grad_error(f, x):
    """f: тензор float64 -> скаляр-тензор; x - тензор float64 формы (d,).

    Вернуть max |autograd-градиент - центральная разность| по всем координатам; шаг eps = 1e-6. x не менять. Вернуть float.
    """
    # TODO
    return 1.0
