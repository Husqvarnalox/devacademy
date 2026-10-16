import numpy as np


def _sigmoid(z):
    z = np.asarray(z, dtype=np.float64)
    out = np.empty_like(z)
    pos = z >= 0
    out[pos] = 1.0 / (1.0 + np.exp(-z[pos]))
    e = np.exp(z[~pos])
    out[~pos] = e / (1.0 + e)
    return out


def loss_l2(Xb, y, w, lam):
    """mean(logaddexp(0, z) - y z) + 0.5 * lam * sum(w[1:]^2), z = Xb w. Смещение w[0] не штрафуется."""
    z = Xb @ w
    return float(np.mean(np.logaddexp(0.0, z) - y * z) + 0.5 * lam * np.sum(w[1:] ** 2))


def gradient_l2(Xb, y, w, lam):
    """Градиент loss_l2: Xb^T (sigmoid(Xb w) - y) / n + lam * [0, w[1:]]."""
    g = Xb.T @ (_sigmoid(Xb @ w) - y) / Xb.shape[0]
    g[1:] += lam * w[1:]
    return g


def fit_l2(X, y, lr, epochs, lam):
    """Пакетный спуск для loss_l2, начало w = 0. X без смещения; верните w формы (d + 1,)."""
    Xb = np.hstack([np.ones((X.shape[0], 1)), np.asarray(X, dtype=np.float64)])
    w = np.zeros(Xb.shape[1])
    for _ in range(epochs):
        w = w - lr * gradient_l2(Xb, y, w, lam)
    return w
