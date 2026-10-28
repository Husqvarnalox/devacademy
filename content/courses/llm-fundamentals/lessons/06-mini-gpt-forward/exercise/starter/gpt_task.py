import numpy as np


def init_params(vocab_size, d_model, n_heads, n_layers, max_len, seed=0):
    """Случайные веса мини-GPT (уже реализовано). Все массивы float64, веса ~ N(0, 0.02^2), гейны LayerNorm = 1, смещения = 0."""
    if d_model % n_heads != 0:
        raise ValueError("d_model должно делиться на n_heads")
    rng = np.random.default_rng(seed)

    def w(*shape):
        return rng.normal(0.0, 0.02, size=shape)

    blocks = []
    for _ in range(n_layers):
        blocks.append({
            "ln1_g": np.ones(d_model), "ln1_b": np.zeros(d_model),
            "w_qkv": w(d_model, 3 * d_model), "b_qkv": np.zeros(3 * d_model),
            "w_o": w(d_model, d_model), "b_o": np.zeros(d_model),
            "ln2_g": np.ones(d_model), "ln2_b": np.zeros(d_model),
            "w_fc": w(d_model, 4 * d_model), "b_fc": np.zeros(4 * d_model),
            "w_proj": w(4 * d_model, d_model), "b_proj": np.zeros(d_model),
        })
    return {
        "n_heads": n_heads,
        "wte": w(vocab_size, d_model),
        "wpe": w(max_len, d_model),
        "blocks": blocks,
        "lnf_g": np.ones(d_model), "lnf_b": np.zeros(d_model),
    }


def causal_self_attention(x, w_qkv, b_qkv, w_o, b_o, n_heads):
    """Многоголовое внимание с причинной маской (уже реализовано, это навык предыдущего урока). x: (T, d) -> (T, d)."""
    T, d = x.shape
    dh = d // n_heads
    qkv = x @ w_qkv + b_qkv
    q, k, v = np.split(qkv, 3, axis=-1)
    q = q.reshape(T, n_heads, dh).transpose(1, 0, 2)
    k = k.reshape(T, n_heads, dh).transpose(1, 0, 2)
    v = v.reshape(T, n_heads, dh).transpose(1, 0, 2)
    scores = q @ k.transpose(0, 2, 1) / np.sqrt(dh)
    scores = np.where(np.tril(np.ones((T, T), dtype=bool)), scores, -np.inf)
    scores = scores - scores.max(axis=-1, keepdims=True)
    weights = np.exp(scores)
    weights = weights / weights.sum(axis=-1, keepdims=True)
    out = (weights @ v).transpose(1, 0, 2).reshape(T, d)
    return out @ w_o + b_o


def layer_norm(x, g, b, eps=1e-5):
    """LayerNorm по последней оси, для каждой строки отдельно: (x - mean) / sqrt(var + eps) * g + b.
    var - смещённая дисперсия (делится на d, как np.var по умолчанию). g, b - массивы формы (d,)."""
    # TODO
    return x


def gelu(x):
    """GELU, tanh-приближение из GPT-2: 0.5 * x * (1 + tanh(sqrt(2 / pi) * (x + 0.044715 * x ** 3))). Поэлементно."""
    # TODO
    return x


def mlp(x, block):
    """Поточечная сеть блока: gelu(x @ w_fc + b_fc) @ w_proj + b_proj, где w_fc: (d, 4d), w_proj: (4d, d). (T, d) -> (T, d)."""
    # TODO
    return x


def block_forward(x, block, n_heads):
    """Блок с предварительной нормализацией (pre-LN), x: (T, d) -> (T, d):
        x = x + causal_self_attention(layer_norm(x, ln1_g, ln1_b), w_qkv, b_qkv, w_o, b_o, n_heads)
        x = x + mlp(layer_norm(x, ln2_g, ln2_b), block)
    """
    # TODO
    return x


def forward(ids, params):
    """Прямой проход: id токенов (T,) -> логиты (T, V), float64.

    x = wte[ids] + wpe[:T]; затем все блоки по порядку (params["blocks"], n_heads = params["n_heads"]);
    затем LayerNorm с lnf_g, lnf_b; логиты = x @ wte.T (веса входных эмбеддингов используются повторно).
    ValueError: ids пуст, T больше max_len (число строк wpe), есть id < 0 или >= V.
    """
    # TODO
    return np.zeros((len(ids), params["wte"].shape[0]))
