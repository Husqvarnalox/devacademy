import numpy as np


def softmax(x, axis=-1):
    """Устойчивый softmax по оси axis (вычитание максимума). Значения -inf получают вероятность 0.

    Достаточно, чтобы в каждой строке было хотя бы одно конечное значение.
    """
    x = np.asarray(x, dtype=np.float64)
    e = np.exp(x - np.max(x, axis=axis, keepdims=True))
    return e / e.sum(axis=axis, keepdims=True)


def causal_mask(T):
    """Булева матрица (T, T): True там, где вниманию разрешено смотреть (j <= i)."""
    return np.tril(np.ones((T, T), dtype=bool))


def scaled_dot_product_attention(Q, K, V, mask=None):
    """Q (..., Tq, d_k), K (..., Tk, d_k), V (..., Tk, d_v) -> (out, weights).

    scores = Q @ K.swapaxes(-1, -2) / sqrt(d_k); там, где mask равна False, scores = -inf;
    weights = softmax(scores) по последней оси; out = weights @ V. Должно работать с ведущими осями (головы, батч).
    """
    Q = np.asarray(Q, dtype=np.float64)
    K = np.asarray(K, dtype=np.float64)
    V = np.asarray(V, dtype=np.float64)
    scores = Q @ K.swapaxes(-1, -2) / np.sqrt(Q.shape[-1])
    if mask is not None:
        scores = np.where(mask, scores, -np.inf)
    weights = softmax(scores, axis=-1)
    return weights @ V, weights


def split_heads(X, n_heads):
    """(T, d) -> (n_heads, T, d // n_heads); голова h - столбцы h * dh : (h + 1) * dh.

    Если d не делится на n_heads (или n_heads < 1) - ValueError.
    """
    X = np.asarray(X, dtype=np.float64)
    T, d = X.shape
    if n_heads < 1 or d % n_heads != 0:
        raise ValueError(f"d={d} не делится на n_heads={n_heads}")
    return X.reshape(T, n_heads, d // n_heads).transpose(1, 0, 2)


def merge_heads(H):
    """Обратная операция: (n_heads, T, dh) -> (T, n_heads * dh)."""
    H = np.asarray(H, dtype=np.float64)
    n_heads, T, dh = H.shape
    return H.transpose(1, 0, 2).reshape(T, n_heads * dh)


def multi_head_attention(X, Wq, Wk, Wv, Wo, n_heads, causal=True):
    """Многоголовое внимание: Q = X @ Wq, K = X @ Wk, V = X @ Wv; разбить на головы; внимание
    (с причинной маской, если causal); склеить головы; умножить на Wo. X (T, d) -> (T, d)."""
    X = np.asarray(X, dtype=np.float64)
    Q = split_heads(X @ Wq, n_heads)
    K = split_heads(X @ Wk, n_heads)
    V = split_heads(X @ Wv, n_heads)
    mask = causal_mask(X.shape[0]) if causal else None
    out, _ = scaled_dot_product_attention(Q, K, V, mask)
    return merge_heads(out) @ Wo
