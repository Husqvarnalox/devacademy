import numpy as np


def add_bias(X):
    X = np.asarray(X, dtype=np.float64)
    return np.hstack([np.ones((X.shape[0], 1)), X])


def mse(Xb, y, w):
    r = Xb @ w - y
    return float(np.mean(r * r))


def fit_normal(X, y):
    Xb = add_bias(X)
    return np.linalg.solve(Xb.T @ Xb, Xb.T @ np.asarray(y, dtype=np.float64))


def fit_lstsq(X, y):
    Xb = add_bias(X)
    return np.linalg.lstsq(Xb, np.asarray(y, dtype=np.float64), rcond=None)[0]


def fit_gd(X, y, lr, epochs):
    Xb = add_bias(X)
    y = np.asarray(y, dtype=np.float64)
    n = Xb.shape[0]
    w = np.zeros(Xb.shape[1])
    history = np.empty(epochs + 1)
    history[0] = mse(Xb, y, w)
    with np.errstate(all="ignore"):
        for k in range(epochs):
            grad = (2.0 / n) * (Xb.T @ (Xb @ w - y))
            w = w - lr * grad
            history[k + 1] = mse(Xb, y, w)
    return w, history
