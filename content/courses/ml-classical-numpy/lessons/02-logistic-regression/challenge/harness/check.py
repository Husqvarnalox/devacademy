import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import l2_task as m


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


def blobs(seed=0, n=100, sep=1.5):
    rng = np.random.default_rng(seed)
    X = np.vstack([rng.normal(-sep, 1.0, (n, 2)), rng.normal(sep, 1.0, (n, 2))])
    y = np.concatenate([np.zeros(n), np.ones(n)])
    return X, y


def bias(X):
    return np.hstack([np.ones((len(X), 1)), X])


def t_loss_values():
    Xb = np.array([[1.0, 1.0], [1.0, -1.0]])
    y = np.array([1.0, 0.0])
    w = np.array([5.0, 2.0])               # смещение 5 не должно штрафоваться
    z = Xb @ w
    base = np.mean(np.log1p(np.exp(z)) - y * z)
    want = base + 0.5 * 3.0 * 4.0
    got = m.loss_l2(Xb, y, w, 3.0)
    if not np.isclose(got, want, rtol=1e-12, atol=0):
        return f"loss_l2: ожидалось {want!r} (штраф 0.5 * lam * sum(w[1:]^2), смещение не штрафуется), получили {got!r}"


def t_gradient_finite_diff():
    X, y = blobs(1, n=15)
    Xb = bias(X)
    w = np.array([0.4, -0.7, 0.9])
    lam = 0.7
    g = m.gradient_l2(Xb, y, w, lam)
    num = np.empty(3)
    for j in range(3):
        e = np.zeros(3)
        e[j] = 1e-6
        num[j] = (m.loss_l2(Xb, y, w + e, lam) - m.loss_l2(Xb, y, w - e, lam)) / 2e-6
    if g.shape != (3,) or not np.allclose(g, num, rtol=0, atol=1e-6):
        return f"градиент не совпал с конечными разностями loss_l2: {g!r} против {num!r}"


def t_fit_l2_converges():
    X, y = blobs(0)
    Xb = bias(X)
    w = m.fit_l2(X, y, 0.5, 2000, 0.1)
    g = m.gradient_l2(Xb, y, w, 0.1)
    if w.shape != (3,) or np.linalg.norm(g) > 1e-4:
        return f"после 2000 эпох норма градиента должна быть < 1e-4, получили {np.linalg.norm(g):.2e}"


def t_lambda_shrinks():
    X, y = blobs(0)
    n0 = np.linalg.norm(m.fit_l2(X, y, 0.5, 1500, 0.0)[1:])
    n1 = np.linalg.norm(m.fit_l2(X, y, 0.5, 1500, 0.3)[1:])
    n2 = np.linalg.norm(m.fit_l2(X, y, 0.5, 1500, 3.0)[1:])
    if not (n0 > n1 > n2):
        return f"норма весов должна убывать с ростом lam: {n0:.3f}, {n1:.3f}, {n2:.3f}"


def t_bias_not_penalized():
    rng = np.random.default_rng(5)
    X = rng.normal(size=(300, 1))
    y = np.ones(300)
    y[:20] = 0.0                         # почти все метки 1, нужен большой положительный сдвиг
    w_small = m.fit_l2(X, y, 0.5, 3000, 0.0)
    w_big = m.fit_l2(X, y, 0.02, 5000, 50.0)
    if not (w_big[0] > 1.0 and abs(w_big[1]) < 0.2 and abs(w_small[1]) > abs(w_big[1])):
        return f"при сильной регуляризации наклон должен почти исчезнуть, а смещение - остаться большим (его не штрафуют): {w_big!r}"


def t_separable_bounded():
    X = np.array([[-2.0], [-1.0], [1.0], [2.0]])
    y = np.array([0.0, 0.0, 1.0, 1.0])
    w0 = m.fit_l2(X, y, 1.0, 5000, 0.0)
    w1 = m.fit_l2(X, y, 1.0, 5000, 1.0)
    if not (np.isfinite(w1).all() and w1[1] > 0 and abs(w1[1]) < abs(w0[1]) and abs(w1[1]) < 1.0):
        return f"на разделимых данных штраф должен ограничивать вес: без штрафа {w0!r}, со штрафом {w1!r}"


case("loss-values", t_loss_values)
case("gradient-finite-diff", t_gradient_finite_diff)
case("fit-l2-converges", t_fit_l2_converges)
case("lambda-shrinks", t_lambda_shrinks)
case("bias-not-penalized", t_bias_not_penalized)
case("separable-bounded", t_separable_bounded)
