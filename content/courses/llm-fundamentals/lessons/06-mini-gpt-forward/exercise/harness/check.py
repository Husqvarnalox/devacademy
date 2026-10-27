import json
import os
import warnings

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import gpt_task as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() возвращает None при успехе или строку с причиной провала; исключения - провал."""
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


# ---- независимая эталонная реализация (не использует код ученика) ----

def ref_ln(x, g, b, eps=1e-5):
    mu = x.mean(-1, keepdims=True)
    var = ((x - mu) ** 2).mean(-1, keepdims=True)
    return (x - mu) / np.sqrt(var + eps) * g + b


def ref_gelu(x):
    return 0.5 * x * (1 + np.tanh(np.sqrt(2 / np.pi) * (x + 0.044715 * x ** 3)))


def ref_attn(x, blk, n_heads):
    T, d = x.shape
    dh = d // n_heads
    qkv = x @ blk["w_qkv"] + blk["b_qkv"]
    out = np.zeros((T, d))
    for h in range(n_heads):
        q = qkv[:, h * dh:(h + 1) * dh]
        k = qkv[:, d + h * dh:d + (h + 1) * dh]
        v = qkv[:, 2 * d + h * dh:2 * d + (h + 1) * dh]
        s = q @ k.T / np.sqrt(dh)
        for i in range(T):
            row = s[i, :i + 1]
            e = np.exp(row - row.max())
            out[i, h * dh:(h + 1) * dh] = (e / e.sum()) @ v[:i + 1]
    return out @ blk["w_o"] + blk["b_o"]


def ref_forward(ids, p):
    x = p["wte"][list(ids)] + p["wpe"][:len(ids)]
    for blk in p["blocks"]:
        x = x + ref_attn(ref_ln(x, blk["ln1_g"], blk["ln1_b"]), blk, p["n_heads"])
        h = ref_ln(x, blk["ln2_g"], blk["ln2_b"])
        x = x + ref_gelu(h @ blk["w_fc"] + blk["b_fc"]) @ blk["w_proj"] + blk["b_proj"]
    x = ref_ln(x, p["lnf_g"], p["lnf_b"])
    return x @ p["wte"].T


def strong_params(vocab=11, d=8, heads=2, layers=2, max_len=9, seed=3, scale=0.4):
    """Те же формы, что у init_params, но веса побольше и гейны/смещения случайные: так ошибки не прячутся в малых числах."""
    p = m.init_params(vocab, d, heads, layers, max_len, seed=seed)
    rng = np.random.default_rng(seed + 100)

    p["wte"] = rng.normal(0.0, scale, size=p["wte"].shape)
    p["wpe"] = rng.normal(0.0, scale, size=p["wpe"].shape)
    p["lnf_g"] = rng.normal(1.0, 0.2, size=d)
    p["lnf_b"] = rng.normal(0.0, 0.2, size=d)
    for blk in p["blocks"]:
        for key, a in blk.items():
            if key.endswith("_g"):
                blk[key] = rng.normal(1.0, 0.2, size=a.shape)
            else:
                blk[key] = rng.normal(0.0, scale, size=a.shape)
    return p


def t_layer_norm():
    rng = np.random.default_rng(0)
    x = rng.normal(3.0, 5.0, size=(6, 16))
    y = m.layer_norm(x, np.ones(16), np.zeros(16))
    if y.shape != x.shape:
        return f"форма результата должна совпадать с формой входа {x.shape}, получили {y.shape}"
    if not np.allclose(y.mean(axis=-1), 0.0, atol=1e-9):
        return "после LayerNorm среднее каждой строки должно быть около 0"
    if not np.allclose(y.var(axis=-1), 1.0, atol=1e-4):
        return "после LayerNorm дисперсия каждой строки должна быть около 1 (по последней оси, для каждой строки отдельно)"
    g, b = rng.normal(1.0, 0.3, size=16), rng.normal(0.0, 0.3, size=16)
    got = m.layer_norm(x, g, b)
    if not np.allclose(got, ref_ln(x, g, b), atol=1e-10):
        return "с гейном g и смещением b результат должен быть (x - mean) / sqrt(var + eps) * g + b"


def t_gelu():
    x = np.array([-3.0, -1.0, 0.0, 0.5, 1.0, 2.0])
    want = np.array([-0.003637, -0.158808, 0.0, 0.345714, 0.841192, 1.954597])
    got = np.asarray(m.gelu(x))
    if got.shape != x.shape or not np.allclose(got, want, atol=1e-5):
        return f"gelu({x.tolist()}): ожидалось около {want.tolist()}, получили {got.tolist()}"
    if float(m.gelu(np.array(0.0))) != 0.0:
        return "gelu(0) должна быть ровно 0"


def t_forward_shape():
    p = m.init_params(13, 8, 2, 2, 10, seed=1)
    ids = [1, 5, 2, 7]
    out = m.forward(ids, p)
    if not isinstance(out, np.ndarray) or out.shape != (4, 13):
        return f"forward должна вернуть массив формы (T, V) = (4, 13), получили {getattr(out, 'shape', type(out))}"
    if out.dtype != np.float64 or not np.all(np.isfinite(out)):
        return "логиты должны быть конечными числами float64"
    one = m.forward([3], p)
    if one.shape != (1, 13):
        return f"для одного токена ожидалась форма (1, 13), получили {one.shape}"


def t_layer_norm_edge():
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        y = m.layer_norm(np.full((2, 5), 7.0), np.full(5, 2.0), np.full(5, 0.5))
        if not np.allclose(y, 0.5):
            return "для строки из одинаковых чисел (x - mean = 0, var = 0) результат равен смещению b, а не nan: eps стоит под корнем"
        x = np.array([[1.0, 2.0, 3.0, 4.0]])
        want = (x - 2.5) / np.sqrt(1.25 + 1e-5)
        if not np.allclose(m.layer_norm(x, np.ones(4), np.zeros(4)), want, atol=1e-12):
            return "дисперсия смещённая: для [1, 2, 3, 4] var = 1.25 (делим на d = 4, не на 3)"
        y3 = m.layer_norm(np.arange(24.0).reshape(2, 3, 4), np.ones(4), np.zeros(4))
        if y3.shape != (2, 3, 4) or not np.allclose(y3.mean(axis=-1), 0.0, atol=1e-9):
            return "нормализация идёт по последней оси и для массивов с лишними осями"


def t_gelu_edge():
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        big = np.array([-60.0, -10.0, 10.0, 60.0])
        got = np.asarray(m.gelu(big))
        if not np.allclose(got, [0.0, 0.0, 10.0, 60.0], atol=1e-6):
            return f"для больших |x| gelu(x) ~ 0 при x << 0 и ~ x при x >> 0, получили {got.tolist()}"
        mid = np.array([[0.3, -0.7], [1.5, -2.2]])
        if not np.allclose(m.gelu(mid), ref_gelu(mid), atol=1e-12):
            return "gelu должна работать поэлементно на массивах любой формы"


def t_mlp_block():
    p = strong_params()
    blk = p["blocks"][0]
    rng = np.random.default_rng(5)
    x = rng.normal(size=(5, 8))
    got = m.mlp(x, blk)
    want = ref_gelu(x @ blk["w_fc"] + blk["b_fc"]) @ blk["w_proj"] + blk["b_proj"]
    if got.shape != (5, 8) or not np.allclose(got, want, atol=1e-10):
        return "mlp(x) = gelu(x @ w_fc + b_fc) @ w_proj + b_proj, форма (T, d)"
    attn = ref_attn(ref_ln(x, blk["ln1_g"], blk["ln1_b"]), blk, p["n_heads"])
    y = x + attn
    y = y + ref_gelu(ref_ln(y, blk["ln2_g"], blk["ln2_b"]) @ blk["w_fc"] + blk["b_fc"]) @ blk["w_proj"] + blk["b_proj"]
    got_block = m.block_forward(x, blk, p["n_heads"])
    if not np.allclose(got_block, y, atol=1e-10):
        return "block_forward: x = x + attn(ln1(x)); затем x = x + mlp(ln2(x)); нормализация стоит перед подслоем (pre-LN)"


def t_block_residual():
    p = strong_params()
    blk = dict(p["blocks"][1])
    for key in ("w_o", "b_o", "w_proj", "b_proj"):
        blk[key] = np.zeros_like(blk[key])
    x = np.random.default_rng(6).normal(size=(6, 8))
    got = m.block_forward(x, blk, p["n_heads"])
    if not np.allclose(got, x, atol=1e-12):
        return "если выходные матрицы внимания и MLP равны нулю, блок обязан вернуть x без изменений (остаточная связь x + ...)"


def t_forward_reference():
    p = strong_params()
    for ids in ([0], [3, 3, 3], [10, 0, 5, 2, 7, 1, 9, 4, 6]):
        got = m.forward(ids, p)
        want = ref_forward(ids, p)
        if got.shape != want.shape or not np.allclose(got, want, atol=1e-9):
            return f"forward({ids}) отличается от эталона: макс. расхождение {np.max(np.abs(got - want)) if got.shape == want.shape else 'форма'}"
    p2 = m.init_params(9, 12, 3, 3, 6, seed=2)
    ids = [1, 2, 3, 4]
    if not np.allclose(m.forward(ids, p2), ref_forward(ids, p2), atol=1e-12):
        return "forward на init_params(9, 12, 3, 3, 6) не совпадает с эталоном"
    if not np.allclose(m.forward(np.array([4, 4, 1]), p), ref_forward([4, 4, 1], p), atol=1e-9):
        return "ids может быть и np.ndarray"


def t_causality():
    p = strong_params()
    ids = [4, 1, 8, 8, 2, 0, 6]
    full = m.forward(ids, p)
    for k in range(1, len(ids)):
        part = m.forward(ids[:k], p)
        if not np.allclose(part, full[:k], atol=1e-10):
            return f"логиты первых {k} позиций не должны зависеть от последующих токенов: forward(ids[:{k}]) != forward(ids)[:{k}]"
    changed = list(ids)
    changed[-1] = 9
    if not np.allclose(m.forward(changed, p)[:-1], full[:-1], atol=1e-10):
        return "замена последнего токена не должна менять логиты предыдущих позиций"
    if np.allclose(m.forward(changed, p)[-1], full[-1], atol=1e-6):
        return "а логиты последней позиции от последнего токена зависеть обязаны"


def t_forward_deterministic():
    p = strong_params()
    snapshot = {"wte": p["wte"].copy(), "w": p["blocks"][0]["w_qkv"].copy()}
    ids = [2, 7, 7, 1]
    a = m.forward(ids, p)
    b = m.forward(ids, p)
    if not np.array_equal(a, b):
        return "forward должна быть детерминированной: один вход - один результат"
    if not np.array_equal(p["wte"], snapshot["wte"]) or not np.array_equal(p["blocks"][0]["w_qkv"], snapshot["w"]):
        return "forward не должна изменять веса модели"
    if not np.array_equal(m.forward(list(ids), p), m.forward(tuple(ids), p)):
        return "список и кортеж id дают один результат"


def t_forward_errors():
    p = m.init_params(10, 8, 2, 1, 5, seed=0)
    bad = {
        "пустой список": [],
        "длиннее max_len": [1, 2, 3, 4, 5, 6],
        "id равен размеру словаря": [1, 10],
        "отрицательный id": [-1, 2],
    }
    for name, ids in bad.items():
        try:
            m.forward(ids, p)
        except ValueError:
            pass
        else:
            return f"{name}: ожидался ValueError"
    try:
        out = m.forward([9, 0, 1, 2, 3], p)
    except ValueError:
        return "длина ровно max_len и id = V - 1 допустимы"
    if out.shape != (5, 10):
        return "для граничного случая ожидалась форма (5, 10)"


case("layer-norm", t_layer_norm)
case("gelu-values", t_gelu)
case("forward-shape", t_forward_shape)
case("layer-norm-edge", t_layer_norm_edge)
case("gelu-edge", t_gelu_edge)
case("mlp-and-block", t_mlp_block)
case("block-residual", t_block_residual)
case("forward-reference", t_forward_reference)
case("forward-causality", t_causality)
case("forward-deterministic", t_forward_deterministic)
case("forward-errors", t_forward_errors)
