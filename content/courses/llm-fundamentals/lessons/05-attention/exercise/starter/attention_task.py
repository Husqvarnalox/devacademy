import numpy as np


def softmax(x, axis=-1):
    """Устойчивый softmax по оси axis (вычитание максимума). Значения -inf получают вероятность 0.

    Достаточно, чтобы в каждой строке было хотя бы одно конечное значение.
    """
    # TODO
    e = np.exp(np.asarray(x, dtype=np.float64))
    return e / e.sum(axis=axis, keepdims=True)


def causal_mask(T):
    """Булева матрица (T, T): True там, где вниманию разрешено смотреть (j <= i)."""
    # TODO
    return np.ones((T, T), dtype=bool)


def scaled_dot_product_attention(Q, K, V, mask=None):
    """Q (..., Tq, d_k), K (..., Tk, d_k), V (..., Tk, d_v) -> (out, weights).

    scores = Q @ K.swapaxes(-1, -2) / sqrt(d_k); там, где mask равна False, scores = -inf;
    weights = softmax(scores) по последней оси; out = weights @ V. Должно работать с ведущими осями (головы, батч).
    """
    # TODO
    Q, K, V = (np.asarray(a, dtype=np.float64) for a in (Q, K, V))
    return np.zeros(Q.shape[:-1] + (V.shape[-1],)), np.zeros(Q.shape[:-1] + (K.shape[-2],))


def split_heads(X, n_heads):
    """(T, d) -> (n_heads, T, d // n_heads); голова h - столбцы h * dh : (h + 1) * dh.

    Если d не делится на n_heads (или n_heads < 1) - ValueError.
    """
    # TODO
    X = np.asarray(X, dtype=np.float64)
    return X.reshape(n_heads, X.shape[0], -1)


def merge_heads(H):
    """Обратная операция: (n_heads, T, dh) -> (T, n_heads * dh)."""
    # TODO
    H = np.asarray(H, dtype=np.float64)
    return H.reshape(H.shape[1], -1)


def multi_head_attention(X, Wq, Wk, Wv, Wo, n_heads, causal=True):
    """Многоголовое внимание: Q = X @ Wq, K = X @ Wk, V = X @ Wv; разбить на головы; внимание
    (с причинной маской, если causal); склеить головы; умножить на Wo. X (T, d) -> (T, d)."""
    # TODO
    return np.asarray(X, dtype=np.float64) @ Wo
