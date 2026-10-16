import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import linreg_task as m


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

W_TRUE = np.array([1.5, 2.0, -1.0, 0.5])


def make_data(seed=0, n=120, noise=0.05):
    rng = np.random.default_rng(seed)
    X = rng.normal(size=(n, 3))
    y = W_TRUE[0] + X @ W_TRUE[1:] + noise * rng.normal(size=n)
    return X, y


def t_add_bias():
    X = np.array([[1, 2], [3, 4], [5, 6]])
    Xb = m.add_bias(X)
    if Xb.shape != (3, 3) or Xb.dtype != np.float64 or not np.array_equal(Xb, [[1, 1, 2], [1, 3, 4], [1, 5, 6]]):
        return f"add_bias: ожидалась матрица [[1,1,2],[1,3,4],[1,5,6]] float64, получили {Xb!r}"


def t_mse():
    Xb = np.array([[1.0, 0.0], [1.0, 1.0], [1.0, 2.0]])
    y = np.array([1.0, 2.0, 5.0])
    w = np.array([0.0, 1.0])
    got = m.mse(Xb, y, w)
    want = (1 + 1 + 9) / 3
    if not np.isclose(got, want, rtol=1e-12, atol=0):
        return f"mse: ожидалось {want}, получили {got!r}"


def t_fit_lstsq():
    X, y = make_data()
    w = m.fit_lstsq(X, y)
    if w.shape != (4,) or not np.allclose(w, W_TRUE, rtol=0, atol=0.05):
        return f"ожидались коэффициенты около {W_TRUE.tolist()} (допуск 0.05), получили {w!r}"


def t_fit_normal_matches_lstsq():
    X, y = make_data(seed=1)
    wn = m.fit_normal(X, y)
    wl = m.fit_lstsq(X, y)
    if wn.shape != (4,) or not np.allclose(wn, wl, rtol=0, atol=1e-8):
        return f"нормальные уравнения должны совпасть с lstsq на хорошо обусловленных данных (атол 1e-8): {wn!r} против {wl!r}"


def t_fit_gd_converges():
    X, y = make_data(seed=2)
    w, hist = m.fit_gd(X, y, 0.1, 1500)
    wl = m.fit_lstsq(X, y)
    if w.shape != (4,) or hist.shape != (1501,):
        return f"ожидались w формы (4,) и history формы (1501,), получили {w.shape}, {hist.shape}"
    if not np.allclose(w, wl, rtol=0, atol=1e-6):
        return f"градиентный спуск (lr=0.1, 1500 эпох) должен сойтись к lstsq с допуском 1e-6: {w!r} против {wl!r}"


def t_gd_history_monotone():
    X, y = make_data(seed=3)
    w, hist = m.fit_gd(X, y, 0.05, 50)
    Xb = np.hstack([np.ones((len(X), 1)), X])
    if not np.isclose(hist[0], np.mean(y ** 2), rtol=1e-10, atol=0):
        return f"history[0] - это MSE при w = 0, то есть mean(y^2) = {np.mean(y ** 2):.6f}; получили {hist[0]!r}"
    if not np.isclose(hist[-1], np.mean((Xb @ w - y) ** 2), rtol=1e-10, atol=0):
        return "history[-1] должно совпадать с MSE итоговых весов"
    if not (np.diff(hist) <= 1e-12).all():
        return "при маленьком lr MSE не должна расти от шага к шагу; проверьте знак шага и множитель градиента"


def t_collinear_lstsq():
    rng = np.random.default_rng(4)
    a = rng.normal(size=(60, 2))
    X = np.hstack([a, a[:, :1]])           # третий столбец дублирует первый
    y = 1.0 + 2.0 * a[:, 0] - 3.0 * a[:, 1]
    w = m.fit_lstsq(X, y)
    Xb = np.hstack([np.ones((60, 1)), X])
    if w.shape != (4,) or not np.allclose(Xb @ w, y, rtol=0, atol=1e-8):
        return "для вырожденных признаков lstsq должен давать веса, точно воспроизводящие y (допуск 1e-8)"


def t_ill_conditioned_lstsq():
    x = np.linspace(0.0, 1.0, 50)
    X = np.vander(x, 9, increasing=True)[:, 1:]       # x, x^2, ..., x^8
    w_true = np.arange(1.0, 9.0)
    y = 0.5 + X @ w_true
    w = m.fit_lstsq(X, y)
    if w.shape != (9,) or not np.allclose(w[1:], w_true, rtol=0, atol=1e-6) or abs(w[0] - 0.5) > 1e-6:
        return f"lstsq на полиномиальных признаках должен восстановить коэффициенты с допуском 1e-6, получили {w!r}"


def t_gd_edge_cases():
    X, y = make_data(seed=5, n=40)
    w0, h0 = m.fit_gd(X, y, 0.1, 0)
    if not (np.array_equal(w0, np.zeros(4)) and h0.shape == (1,)):
        return f"при epochs=0 ожидались w = нули и history из одного значения, получили {w0!r}, {h0!r}"
    w, h = m.fit_gd(X, y, 5.0, 30)
    if not (h[-1] > 100 * h[0] or not np.isfinite(h[-1])):
        return "при слишком большом lr (5.0) пакетный градиентный спуск должен расходиться"


case("add-bias", t_add_bias)
case("mse", t_mse)
case("fit-lstsq", t_fit_lstsq)
case("fit-normal-matches-lstsq", t_fit_normal_matches_lstsq)
case("fit-gd-converges", t_fit_gd_converges)
case("gd-history-monotone", t_gd_history_monotone)
case("collinear-lstsq", t_collinear_lstsq)
case("ill-conditioned-lstsq", t_ill_conditioned_lstsq)
case("gd-edge-cases", t_gd_edge_cases)
