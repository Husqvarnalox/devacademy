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
    """LayerNorm по последней оси: (x - mean) / sqrt(var + eps) * g + b, дисперсия смещённая (делится на d)."""
    mean = x.mean(axis=-1, keepdims=True)
    var = x.var(axis=-1, keepdims=True)
    return (x - mean) / np.sqrt(var + eps) * g + b


def gelu(x):
    """GELU, tanh-приближение из GPT-2: 0.5 * x * (1 + tanh(sqrt(2 / pi) * (x + 0.044715 * x ** 3)))."""
    return 0.5 * x * (1.0 + np.tanh(np.sqrt(2.0 / np.pi) * (x + 0.044715 * x ** 3)))


def mlp(x, block):
    """Поточечная сеть блока: gelu(x @ w_fc + b_fc) @ w_proj + b_proj. (T, d) -> (T, d)."""
    return gelu(x @ block["w_fc"] + block["b_fc"]) @ block["w_proj"] + block["b_proj"]


def block_forward(x, block, n_heads):
    """Блок с предварительной нормализацией (pre-LN): x = x + attn(ln1(x)); x = x + mlp(ln2(x))."""
    h = layer_norm(x, block["ln1_g"], block["ln1_b"])
    x = x + causal_self_attention(h, block["w_qkv"], block["b_qkv"], block["w_o"], block["b_o"], n_heads)
    h = layer_norm(x, block["ln2_g"], block["ln2_b"])
    return x + mlp(h, block)


def forward(ids, params):
    """Прямой проход по всей последовательности: id (T,) -> логиты (T, V). Эталон для проверки кэша (уже реализовано)."""
    ids = np.asarray(ids)
    if ids.ndim != 1 or ids.size == 0:
        raise ValueError("ids должен быть непустым одномерным списком")
    T = ids.shape[0]
    vocab_size, max_len = params["wte"].shape[0], params["wpe"].shape[0]
    if T > max_len:
        raise ValueError(f"длина {T} больше max_len = {max_len}")
    if ids.min() < 0 or ids.max() >= vocab_size:
        raise ValueError("id вне словаря")
    x = params["wte"][ids] + params["wpe"][:T]
    for block in params["blocks"]:
        x = block_forward(x, block, params["n_heads"])
    x = layer_norm(x, params["lnf_g"], params["lnf_b"])
    return x @ params["wte"].T


def forward_step(token_id, pos, params, cache=None):
    """Один шаг генерации с KV-кэшем (см. условие). Возвращает (logits (V,), cache)."""
    vocab_size, max_len = params["wte"].shape[0], params["wpe"].shape[0]
    if not 0 <= pos < max_len:
        raise ValueError(f"pos = {pos} вне 0..{max_len - 1}")
    if not 0 <= token_id < vocab_size:
        raise ValueError("id вне словаря")
    n_heads = params["n_heads"]
    d = params["wte"].shape[1]
    dh = d // n_heads
    if cache is None:
        cache = [{"k": np.zeros((n_heads, 0, dh)), "v": np.zeros((n_heads, 0, dh))} for _ in params["blocks"]]
    x = params["wte"][token_id] + params["wpe"][pos]
    for block, layer in zip(params["blocks"], cache):
        h = layer_norm(x, block["ln1_g"], block["ln1_b"])
        q, k, v = np.split(h @ block["w_qkv"] + block["b_qkv"], 3)
        q = q.reshape(n_heads, dh)
        layer["k"] = np.concatenate([layer["k"], k.reshape(n_heads, 1, dh)], axis=1)
        layer["v"] = np.concatenate([layer["v"], v.reshape(n_heads, 1, dh)], axis=1)
        scores = np.einsum("hd,htd->ht", q, layer["k"]) / np.sqrt(dh)
        scores = scores - scores.max(axis=-1, keepdims=True)
        weights = np.exp(scores)
        weights = weights / weights.sum(axis=-1, keepdims=True)
        attn = np.einsum("ht,htd->hd", weights, layer["v"]).reshape(d)
        x = x + attn @ block["w_o"] + block["b_o"]
        x = x + mlp(layer_norm(x, block["ln2_g"], block["ln2_b"]), block)
    x = layer_norm(x, params["lnf_g"], params["lnf_b"])
    return x @ params["wte"].T, cache


def generate_greedy(prompt_ids, params, n_new):
    """Жадная генерация с кэшем: возвращает список из n_new новых id."""
    prompt_ids = list(prompt_ids)
    if not prompt_ids:
        raise ValueError("промпт пуст")
    if n_new < 0 or len(prompt_ids) + n_new > params["wpe"].shape[0]:
        raise ValueError("n_new < 0 или промпт и ответ не помещаются в max_len")
    cache, logits = None, None
    for pos, token in enumerate(prompt_ids):
        logits, cache = forward_step(token, pos, params, cache)
    out = []
    for _ in range(n_new):
        nxt = int(np.argmax(logits))
        out.append(nxt)
        if len(out) < n_new:
            logits, cache = forward_step(nxt, len(prompt_ids) + len(out) - 1, params, cache)
    return out
