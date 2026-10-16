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
    """mean(logaddexp(0, z) - y z) + 0.5 * lam * sum(w[1:]^2), z = Xb w. Смещение w[0] НЕ штрафуется. Верните float."""
    # TODO
    return 0.0


def gradient_l2(Xb, y, w, lam):
    """Градиент loss_l2: Xb^T (sigmoid(Xb w) - y) / n плюс lam * w[1:] для всех весов, кроме смещения."""
    # TODO
    return np.zeros_like(w)


def fit_l2(X, y, lr, epochs, lam):
    """Пакетный спуск для loss_l2, начало w = 0. X без смещения; добавьте столбец единиц слева. Верните w формы (d + 1,)."""
    # TODO
    return np.zeros(X.shape[1] + 1)
