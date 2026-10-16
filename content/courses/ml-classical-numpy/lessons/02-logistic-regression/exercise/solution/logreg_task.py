import numpy as np


def add_bias(X):
    X = np.asarray(X, dtype=np.float64)
    return np.hstack([np.ones((X.shape[0], 1)), X])


def sigmoid(z):
    """Устойчивая сигмоида: без переполнения при больших |z|."""
    z = np.asarray(z, dtype=np.float64)
    out = np.empty_like(z)
    pos = z >= 0
    out[pos] = 1.0 / (1.0 + np.exp(-z[pos]))
    e = np.exp(z[~pos])
    out[~pos] = e / (1.0 + e)
    return out


def log_loss(z, y):
    """Средняя логистическая потеря по логитам z: mean(log(1 + e^z) - y * z)."""
    z = np.asarray(z, dtype=np.float64)
    return float(np.mean(np.logaddexp(0.0, z) - y * z))


def gradient(Xb, y, w):
    """Градиент средней логистической потери: Xb^T (sigmoid(Xb w) - y) / n."""
    return Xb.T @ (sigmoid(Xb @ w) - y) / Xb.shape[0]


def fit(X, y, lr, epochs):
    """Пакетный градиентный спуск, начало w = 0. Верните w формы (d + 1,), w[0] - смещение."""
    Xb = add_bias(X)
    w = np.zeros(Xb.shape[1])
    for _ in range(epochs):
        w = w - lr * gradient(Xb, y, w)
    return w


def predict(X, w, threshold=0.5):
    """Метки 0/1 (int): вероятность >= threshold."""
    p = sigmoid(add_bias(X) @ w)
    return (p >= threshold).astype(np.int64)
