import json
import os
import warnings

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import attention_task as m


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


def close(a, b, tol=1e-9):
    a, b = np.asarray(a, dtype=np.float64), np.asarray(b, dtype=np.float64)
    return a.shape == b.shape and np.allclose(a, b, rtol=0, atol=tol)


# Эталон: независимая реализация с явными циклами по головам и позициям
def ref_softmax_row(row):
    mx = max(v for v in row if v != -np.inf)
    ex = [0.0 if v == -np.inf else float(np.exp(v - mx)) for v in row]
    s = sum(ex)
    return [v / s for v in ex]


def ref_attention_single(Q, K, V, causal):
    T, dk = Q.shape
    out = np.zeros((T, V.shape[1]))
    for i in range(T):
        scores = [float(Q[i] @ K[j]) / np.sqrt(dk) if (j <= i or not causal) else -np.inf for j in range(K.shape[0])]
        w = ref_softmax_row(scores)
        out[i] = sum(w[j] * V[j] for j in range(K.shape[0]))
    return out


def ref_mha(X, Wq, Wk, Wv, Wo, n_heads, causal):
    d = X.shape[1]
    dh = d // n_heads
    Q, K, V = X @ Wq, X @ Wk, X @ Wv
    heads = []
    for h in range(n_heads):
        sl = slice(h * dh, (h + 1) * dh)
        heads.append(ref_attention_single(Q[:, sl], K[:, sl], V[:, sl], causal))
    return np.concatenate(heads, axis=1) @ Wo


def make_weights(seed, d, scale=0.5):
    rng = np.random.default_rng(seed)
    return [rng.normal(scale=scale, size=(d, d)) for _ in range(4)]


def t_sdpa_basic():
    Q = np.array([[1.0, 0.0], [0.0, 1.0]])
    K = np.array([[1.0, 0.0], [0.0, 1.0]])
    V = np.array([[1.0, 2.0], [3.0, 4.0]])
    out, w = m.scaled_dot_product_attention(Q, K, V)
    a = np.exp(1 / np.sqrt(2))
    p = a / (a + 1.0)
    want_w = np.array([[p, 1 - p], [1 - p, p]])
    if not close(w, want_w):
        return f"weights: ожидалось softmax(Q @ K.T / sqrt(d_k)) = {want_w.round(4).tolist()}, получили {np.asarray(w).round(4).tolist()}"
    if not close(out, want_w @ V):
        return f"out = weights @ V: ожидалось {(want_w @ V).round(4).tolist()}, получили {np.asarray(out).round(4).tolist()}"
    out, w = m.scaled_dot_product_attention(Q, K, V, mask=np.array([[True, False], [True, True]]))
    want_w = np.array([[1.0, 0.0], [1 - p, p]])
    if not close(w, want_w) or not close(out[0], V[0]):
        return f"с маской [[True, False], [True, True]] первая строка видит только первый токен: weights ожидалось {want_w.round(4).tolist()}, получили {np.asarray(w).round(4).tolist()}"


def t_causal_mask():
    got = m.causal_mask(3)
    want = np.array([[True, False, False], [True, True, False], [True, True, True]])
    if got.dtype != bool or got.shape != (3, 3) or not np.array_equal(got, want):
        return f"causal_mask(3): ожидалась нижнетреугольная bool-матрица {want.tolist()}, получили {got.tolist()} ({got.dtype})"
    if m.causal_mask(1).tolist() != [[True]]:
        return "causal_mask(1) = [[True]]"


def t_split_merge():
    X = np.arange(12, dtype=float).reshape(3, 4)
    H = m.split_heads(X, 2)
    if H.shape != (2, 3, 2):
        return f"split_heads((3, 4), 2): форма (2, 3, 2), получили {H.shape}"
    if not np.array_equal(H[0], X[:, :2]) or not np.array_equal(H[1], X[:, 2:]):
        return "голова h - столбцы h * dh : (h + 1) * dh: H[0] = X[:, :2], H[1] = X[:, 2:]"
    back = m.merge_heads(H)
    if back.shape != (3, 4) or not np.array_equal(back, X):
        return "merge_heads(split_heads(X, n)) должна вернуть X"


def t_softmax_stable():
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        a = m.softmax(np.array([[1000.0, 0.0], [-1000.0, -1000.0]]))
        b = m.softmax(np.array([[0.0, -np.inf, 1.0], [-np.inf, -np.inf, 5.0]]))
        c = m.softmax(np.array([[1.0, 2.0], [3.0, 4.0]]), axis=0)
    if not close(a, [[1.0, 0.0], [0.5, 0.5]]):
        return f"softmax на больших по модулю значениях: ожидалось [[1, 0], [0.5, 0.5]], получили {a.tolist()}"
    e = np.exp(1.0)
    if not close(b, [[1 / (1 + e), 0.0, e / (1 + e)], [0.0, 0.0, 1.0]]) or b[0, 1] != 0.0:
        return f"значения -inf получают ровно 0: получили {b.tolist()}"
    if not close(c.sum(axis=0), [1.0, 1.0]):
        return "параметр axis задаёт ось, вдоль которой вероятности суммируются в 1"
    s = m.softmax(np.random.default_rng(0).normal(size=(2, 3, 5)))
    if s.shape != (2, 3, 5) or not close(s.sum(axis=-1), np.ones((2, 3))):
        return "softmax работает с массивами любой размерности"


def t_sdpa_props():
    rng = np.random.default_rng(1)
    T, dk, dv = 6, 4, 3
    Q, K, V = rng.normal(size=(T, dk)), rng.normal(size=(T, dk)), rng.normal(size=(T, dv))
    out, w = m.scaled_dot_product_attention(Q, K, V, mask=m.causal_mask(T))
    if out.shape != (T, dv) or w.shape != (T, T):
        return f"формы: out (T, d_v), weights (T, T); получили {out.shape} и {w.shape}"
    if not close(w.sum(axis=-1), np.ones(T)):
        return "каждая строка weights суммируется в 1"
    if np.any(np.triu(w, k=1) != 0.0):
        return "с причинной маской веса выше диагонали равны ровно 0"
    if not close(w[0, 0], 1.0, 1e-15):
        return "первая позиция видит только себя: weights[0, 0] = 1"
    Qh, Kh, Vh = rng.normal(size=(2, 3, 5, dk)), rng.normal(size=(2, 3, 5, dk)), rng.normal(size=(2, 3, 5, dv))
    out, w = m.scaled_dot_product_attention(Qh, Kh, Vh, mask=m.causal_mask(5))
    if out.shape != (2, 3, 5, dv) or w.shape != (2, 3, 5, 5):
        return f"ведущие оси (батч, головы) сохраняются: ожидались (2, 3, 5, {dv}) и (2, 3, 5, 5), получили {out.shape} и {w.shape}"
    for b in range(2):
        for h in range(3):
            o1, w1 = m.scaled_dot_product_attention(Qh[b, h], Kh[b, h], Vh[b, h], mask=m.causal_mask(5))
            if not close(out[b, h], o1) or not close(w[b, h], w1):
                return "результат для массива с ведущими осями должен совпадать с результатом для каждой пары (батч, голова) отдельно"
    Qr, Kr, Vr = rng.normal(size=(2, dk)), rng.normal(size=(7, dk)), rng.normal(size=(7, dv))
    out, w = m.scaled_dot_product_attention(Qr, Kr, Vr)
    if out.shape != (2, dv) or w.shape != (2, 7):
        return f"Tq != Tk: ожидались формы (2, {dv}) и (2, 7), получили {out.shape} и {w.shape}"


def t_sdpa_large():
    rng = np.random.default_rng(2)
    Q, K, V = rng.normal(size=(5, 8)) * 1e3, rng.normal(size=(5, 8)) * 1e3, rng.normal(size=(5, 3))
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        out, w = m.scaled_dot_product_attention(Q, K, V, mask=m.causal_mask(5))
        out2, w2 = m.scaled_dot_product_attention(Q, K, V)
    if not (np.all(np.isfinite(out)) and np.all(np.isfinite(w)) and np.all(np.isfinite(out2))):
        return "на больших значениях scores результат должен остаться конечным (softmax с вычитанием максимума)"
    if not close(w.sum(axis=-1), np.ones(5)) or not close(w2.sum(axis=-1), np.ones(5)):
        return "строки weights суммируются в 1 и на больших значениях"
    ref = ref_attention_single(Q, K, V, True)
    if not close(out, ref, 1e-8):
        return "на больших значениях результат расходится с эталоном"


def t_mha_future():
    d, T = 8, 7
    X = np.random.default_rng(3).normal(size=(T, d))
    Wq, Wk, Wv, Wo = make_weights(4, d)
    base = m.multi_head_attention(X, Wq, Wk, Wv, Wo, 2, causal=True)
    X2 = X.copy()
    X2[4:] = np.random.default_rng(5).normal(size=(T - 4, d)) * 3.0
    changed = m.multi_head_attention(X2, Wq, Wk, Wv, Wo, 2, causal=True)
    if not close(base[:4], changed[:4], 1e-12):
        return "с причинной маской выходы позиций 0..3 не должны зависеть от строк X с номерами 4 и дальше"
    if close(base[4:], changed[4:], 1e-6):
        return "выходы позиций, чьи входы изменились, должны измениться"
    free = m.multi_head_attention(X, Wq, Wk, Wv, Wo, 2, causal=False)
    free2 = m.multi_head_attention(X2, Wq, Wk, Wv, Wo, 2, causal=False)
    if close(free[:4], free2[:4], 1e-6):
        return "без причинной маски (causal=False) ранние позиции видят и поздние"
    if close(base, free, 1e-6):
        return "causal=True и causal=False дают разные результаты"


def t_mha_reference():
    d, T = 8, 6
    X = np.random.default_rng(6).normal(size=(T, d))
    for seed, n_heads in ((7, 1), (8, 2), (9, 4), (10, 8)):
        Wq, Wk, Wv, Wo = make_weights(seed, d)
        for causal in (True, False):
            got = m.multi_head_attention(X, Wq, Wk, Wv, Wo, n_heads, causal=causal)
            want = ref_mha(X, Wq, Wk, Wv, Wo, n_heads, causal)
            if got.shape != (T, d) or not close(got, want, 1e-10):
                return f"n_heads={n_heads}, causal={causal}: результат расходится с эталоном (макс. отклонение {np.abs(got - want).max():.3g})"
    X3 = np.random.default_rng(11).normal(size=(3, 12))
    Wq, Wk, Wv, Wo = make_weights(12, 12)
    if not close(m.multi_head_attention(X3, Wq, Wk, Wv, Wo, 3), ref_mha(X3, Wq, Wk, Wv, Wo, 3, True), 1e-10):
        return "d=12, n_heads=3: результат расходится с эталоном; по умолчанию causal=True"


def t_mha_single_head():
    d, T = 6, 5
    X = np.random.default_rng(13).normal(size=(T, d))
    Wq, Wk, Wv, Wo = make_weights(14, d)
    got = m.multi_head_attention(X, Wq, Wk, Wv, Wo, 1, causal=True)
    Q, K, V = X @ Wq, X @ Wk, X @ Wv
    scores = Q @ K.T / np.sqrt(d)
    scores[np.triu_indices(T, k=1)] = -np.inf
    e = np.exp(scores - scores.max(axis=1, keepdims=True))
    want = (e / e.sum(axis=1, keepdims=True)) @ V @ Wo
    if not close(got, want, 1e-10):
        return "одна голова равна обычному вниманию: softmax(Q K^T / sqrt(d) с маской) @ V @ Wo"


def t_mha_permutation():
    d, T = 8, 6
    X = np.random.default_rng(15).normal(size=(T, d))
    Wq, Wk, Wv, Wo = make_weights(16, d)
    perm = np.random.default_rng(17).permutation(T)
    a = m.multi_head_attention(X, Wq, Wk, Wv, Wo, 4, causal=False)
    b = m.multi_head_attention(X[perm], Wq, Wk, Wv, Wo, 4, causal=False)
    if not close(a[perm], b, 1e-10):
        return "без маски перестановка строк X переставляет строки выхода так же (внимание само по себе не знает порядка позиций)"
    c = m.multi_head_attention(X[perm], Wq, Wk, Wv, Wo, 4, causal=True)
    if close(a[perm], c, 1e-6):
        return "с причинной маской порядок позиций важен, и перестановка меняет результат"


def t_split_errors():
    X = np.zeros((3, 6))
    for n in (4, 5, 0, -1):
        try:
            m.split_heads(X, n)
        except ValueError:
            continue
        return f"split_heads(d=6, n_heads={n}) должна бросать ValueError"
    rng = np.random.default_rng(18)
    H = rng.normal(size=(3, 4, 5))
    back = m.split_heads(m.merge_heads(H), 3)
    if not np.array_equal(back, H):
        return "split_heads(merge_heads(H), n_heads) должна вернуть H"
    if m.merge_heads(H).shape != (4, 15):
        return "merge_heads: (3, 4, 5) -> (4, 15)"
    try:
        m.multi_head_attention(np.zeros((3, 6)), *make_weights(0, 6), 4)
    except ValueError:
        return None
    return "multi_head_attention с d % n_heads != 0 должна бросать ValueError"


case("sdpa-basic", t_sdpa_basic)
case("causal-mask", t_causal_mask)
case("split-merge", t_split_merge)
case("softmax-stable", t_softmax_stable)
case("sdpa-props", t_sdpa_props)
case("sdpa-large", t_sdpa_large)
case("mha-future", t_mha_future)
case("mha-reference", t_mha_reference)
case("mha-single-head", t_mha_single_head)
case("mha-permutation", t_mha_permutation)
case("split-errors", t_split_errors)
