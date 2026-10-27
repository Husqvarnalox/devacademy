import json
import os
import warnings

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import kv_task as m


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


def strong_params(vocab=11, d=8, heads=2, layers=2, max_len=9, seed=3, emb=0.4, pos=0.4, wscale=0.4, bscale=0.4):
    """Те же формы, что у init_params, но веса побольше и гейны/смещения случайные: так ошибки не прячутся в малых числах."""
    p = m.init_params(vocab, d, heads, layers, max_len, seed=seed)
    rng = np.random.default_rng(seed + 100)
    p["wte"] = rng.normal(0.0, emb, size=p["wte"].shape)
    p["wpe"] = rng.normal(0.0, pos, size=p["wpe"].shape)
    p["lnf_g"] = rng.normal(1.0, 0.2, size=d)
    p["lnf_b"] = rng.normal(0.0, 0.2, size=d)
    for blk in p["blocks"]:
        for key, a in blk.items():
            if key.endswith("_g"):
                blk[key] = rng.normal(1.0, 0.2, size=a.shape)
            elif key.startswith("b_") or key.endswith("_b"):
                blk[key] = rng.normal(0.0, bscale, size=a.shape)
            else:
                blk[key] = rng.normal(0.0, wscale, size=a.shape)
    return p


def gen_params(**kw):
    """Параметры, при которых жадная генерация не застревает на одном токене."""
    base = dict(vocab=11, d=8, heads=2, layers=2, max_len=12, seed=3, emb=1.0, pos=1.0, wscale=1.0, bscale=0.05)
    base.update(kw)
    return strong_params(**base)


def t_step_logits():
    p = strong_params()
    ids = [4, 1, 8, 8, 2, 0, 6]
    want = ref_forward(ids, p)
    cache = None
    for pos, tok in enumerate(ids):
        logits, cache = m.forward_step(tok, pos, p, cache)
        logits = np.asarray(logits)
        if logits.shape != (11,):
            return f"logits должны иметь форму (V,) = (11,), получили {logits.shape}"
        if not np.allclose(logits, want[pos], atol=1e-9):
            return f"шаг pos = {pos}: логиты с кэшем отличаются от логитов полного forward (макс. расхождение {np.max(np.abs(logits - want[pos])):.3g})"


def t_cache_shapes():
    p = strong_params(d=8, heads=2, layers=3, max_len=9)
    cache = None
    for pos, tok in enumerate([1, 2, 3, 4]):
        _, cache = m.forward_step(tok, pos, p, cache)
        if not isinstance(cache, list) or len(cache) != 3:
            return "cache - список из n_layers = 3 элементов (по слою)"
        for layer in cache:
            for key in ("k", "v"):
                if np.asarray(layer[key]).shape != (2, pos + 1, 4):
                    return f"после шага pos = {pos} cache[l]['{key}'] должен иметь форму (n_heads, {pos + 1}, dh) = (2, {pos + 1}, 4), получили {np.asarray(layer[key]).shape}"


def t_generate_basic():
    p = gen_params()
    prompt = [3, 7, 1]
    ids = list(prompt)
    for _ in range(5):
        ids.append(int(np.argmax(ref_forward(ids, p)[-1])))
    got = m.generate_greedy(prompt, p, 5)
    if list(got) != ids[len(prompt):]:
        return f"generate_greedy({prompt}, p, 5): ожидалось {ids[len(prompt):]}, получили {list(got)}"
    if not all(isinstance(t, int) for t in got):
        return "элементы результата должны быть int"


def t_cache_prefix_stable():
    p = strong_params()
    cache = None
    snaps = []
    for pos, tok in enumerate([5, 0, 9, 2]):
        _, cache = m.forward_step(tok, pos, p, cache)
        snaps.append([(np.array(l["k"]), np.array(l["v"])) for l in cache])
    for i in range(len(snaps) - 1):
        for (k0, v0), (k1, v1) in zip(snaps[i], snaps[i + 1]):
            t = k0.shape[1]
            if not (np.array_equal(k1[:, :t], k0) and np.array_equal(v1[:, :t], v0)):
                return "уже записанные в кэш ключи и значения прошлых позиций не должны меняться: новый шаг только дописывает"


def t_step_errors():
    p = strong_params(max_len=4)
    try:
        m.forward_step(1, 4, p, None)
    except ValueError:
        pass
    else:
        return "pos >= max_len: ожидался ValueError"
    try:
        m.forward_step(1, -1, p, None)
    except ValueError:
        pass
    else:
        return "pos < 0: ожидался ValueError"
    for bad in (11, -1):
        try:
            m.forward_step(bad, 0, p, None)
        except ValueError:
            pass
        else:
            return f"token_id = {bad} вне словаря: ожидался ValueError"
    try:
        logits, _ = m.forward_step(10, 3, p, None)
    except ValueError:
        return "pos = max_len - 1 и token_id = V - 1 допустимы"


def t_fresh_cache():
    p = strong_params()
    a, _ = m.forward_step(3, 0, p, None)
    b, c2 = m.forward_step(3, 0, p)
    if not np.allclose(a, b, atol=1e-12):
        return "cache=None (и значение по умолчанию) означает пустой кэш: один и тот же вход даёт одни и те же логиты"
    _, c1 = m.forward_step(3, 0, p, None)
    _, c1 = m.forward_step(5, 1, p, c1)
    _, other = m.forward_step(3, 0, p, None)
    if np.asarray(other[0]["k"]).shape[1] != 1:
        return "каждый вызов с cache=None начинает с пустого кэша; состояние не должно храниться между вызовами в глобальных переменных"


def t_generate_edges():
    p = gen_params(max_len=6)
    if list(m.generate_greedy([2, 3], p, 0)) != []:
        return "n_new = 0 даёт пустой список"
    one = m.generate_greedy([2], p, 1)
    want = int(np.argmax(ref_forward([2], p)[-1]))
    if list(one) != [want]:
        return f"промпт из одного токена, один новый: ожидалось [{want}], получили {list(one)}"
    full = m.generate_greedy([1, 2], p, 4)
    ids = [1, 2]
    for _ in range(4):
        ids.append(int(np.argmax(ref_forward(ids, p)[-1])))
    if list(full) != ids[2:]:
        return "длина промпта + n_new = max_len допустима и совпадает с генерацией без кэша"
    for prompt, n in (([], 2), ([1, 2], 5), ([1], -1)):
        try:
            m.generate_greedy(prompt, p, n)
        except ValueError:
            pass
        else:
            return f"generate_greedy({prompt}, p, {n}): ожидался ValueError"


def t_generate_long():
    p = gen_params(vocab=17, d=12, heads=3, layers=3, max_len=20, seed=8)
    prompt = [16, 0, 4, 9, 9]
    ids = list(prompt)
    for _ in range(12):
        ids.append(int(np.argmax(ref_forward(ids, p)[-1])))
    got = m.generate_greedy(prompt, p, 12)
    if list(got) != ids[len(prompt):]:
        return "на 3 слоях и 3 головах жадная генерация с кэшем должна совпасть с генерацией через полный forward на каждом шаге"


case("step-logits", t_step_logits)
case("cache-shapes", t_cache_shapes)
case("generate-basic", t_generate_basic)
case("cache-prefix-stable", t_cache_prefix_stable)
case("step-errors", t_step_errors)
case("fresh-cache", t_fresh_cache)
case("generate-edges", t_generate_edges)
case("generate-long", t_generate_long)
