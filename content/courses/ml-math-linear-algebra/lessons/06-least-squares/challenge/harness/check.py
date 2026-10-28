import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import lstsq_task as m


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


def with_bias(X):
    return np.column_stack([np.ones(len(X)), X])


def make_data(seed=5, n=40, d=3, noise=0.2):
    rng = np.random.default_rng(seed)
    X = rng.normal(size=(n, d))
    true_w = np.array([1.5, -2.0, 0.5, 3.0][: d + 1])
    y = with_bias(X) @ true_w + noise * rng.normal(size=n)
    return X, y


def t_lstsq_vs_normal():
    X, y = make_data()
    Xb = with_bias(X)
    want = np.linalg.solve(Xb.T @ Xb, Xb.T @ y)
    got = m.fit_lstsq(X, y)
    if not isinstance(got, np.ndarray) or got.shape != (4,):
        return f"ожидался массив формы (4,), получили {type(got).__name__} {getattr(got, 'shape', None)}"
    if not np.allclose(got, want, rtol=0, atol=1e-8):
        return f"веса должны совпасть с решением нормальных уравнений {want.tolist()}, получили {got.tolist()}"


def t_ridge_basic():
    X, y = make_data(seed=8)
    Xb = with_bias(X)
    pen = 3.0 * np.eye(4)
    pen[0, 0] = 0.0
    want = np.linalg.solve(Xb.T @ Xb + pen, Xb.T @ y)
    got = m.fit_ridge(X, y, 3.0)
    if not np.allclose(got, want, rtol=0, atol=1e-9):
        return f"ridge с alpha = 3 должна дать {want.tolist()}, получили {np.asarray(got).tolist()}"
    const = np.full(len(X), 4.0)
    got_c = m.fit_ridge(X, const, 100.0)
    if not np.allclose(got_c, [4.0, 0.0, 0.0, 0.0], rtol=0, atol=1e-8):
        return f"для постоянного y = 4 свободный член не штрафуется: ожидалось [4, 0, 0, 0], получили {np.asarray(got_c).tolist()}"


def t_rank_deficient():
    rng = np.random.default_rng(21)
    a = rng.normal(size=(30, 2))
    X = np.column_stack([a[:, 0], a[:, 1], a[:, 0]])  # третий признак дублирует первый
    y = 2.0 + 1.0 * a[:, 0] - 3.0 * a[:, 1] + 0.1 * rng.normal(size=30)
    got = m.fit_lstsq(X, y)
    want = np.linalg.pinv(with_bias(X)) @ y
    if not np.all(np.isfinite(got)):
        return "для вырожденной матрицы lstsq всё равно возвращает конечное решение"
    if not np.allclose(got, want, rtol=0, atol=1e-8):
        return f"ожидалось решение минимальной нормы {want.tolist()}, получили {got.tolist()}"
    if abs(got[1] - got[3]) > 1e-8:
        return "у дублирующихся признаков в решении минимальной нормы веса равны"
    ridge = m.fit_ridge(X, y, 1.0)
    if not np.all(np.isfinite(ridge)) or abs(ridge[1] - ridge[3]) > 1e-8:
        return "fit_ridge с alpha > 0 разрешима и для дубликатов даёт равные веса"


def t_ridge_to_lstsq():
    X, y = make_data(seed=13, n=50, d=3, noise=0.5)
    a = m.fit_ridge(X, y, 1e-10)
    b = m.fit_lstsq(X, y)
    if not np.allclose(a, b, rtol=0, atol=1e-6):
        return "при alpha -> 0 гребневая регрессия приближается к обычным наименьшим квадратам"
    big = m.fit_ridge(X, y, 1e6)
    if np.linalg.norm(big[1:]) > 1e-2 * np.linalg.norm(b[1:]):
        return "при огромном alpha веса признаков близки к нулю"


def t_r2():
    y = np.array([1.0, 2.0, 3.0, 4.0])
    if abs(m.r2_score(y, y) - 1.0) > 1e-12:
        return "идеальные предсказания дают R^2 = 1"
    if abs(m.r2_score(y, np.full(4, y.mean()))) > 1e-12:
        return "предсказание средним даёт R^2 = 0"
    got = m.r2_score(y, np.array([4.0, 3.0, 2.0, 1.0]))
    if abs(got - (-3.0)) > 1e-12:
        return f"для обратного порядка SS_res = 20, SS_tot = 5, R^2 = -3, получили {got}"
    X, yy = make_data(seed=2)
    pred = with_bias(X) @ m.fit_lstsq(X, yy)
    r2 = m.r2_score(yy, pred)
    if not 0.9 < r2 <= 1.0:
        return f"модель на данных с малым шумом должна иметь R^2 близко к 1, получили {r2}"


def t_r2_constant_target():
    y = np.array([5.0, 5.0, 5.0])
    if m.r2_score(y, y) != 1.0:
        return "при SS_tot = 0 и SS_res = 0 нужно вернуть 1.0"
    if m.r2_score(y, np.array([5.0, 6.0, 5.0])) != 0.0:
        return "при SS_tot = 0 и SS_res > 0 нужно вернуть 0.0"


def t_int_input_no_mutation():
    X = np.array([[0, 1], [1, 0], [2, 3], [3, 1], [4, 5]])
    y = np.array([3, 4, 12, 9, 20])
    X0, y0 = X.copy(), y.copy()
    w1 = m.fit_lstsq(X, y)
    w2 = m.fit_ridge(X, y, 0.5)
    if not (np.array_equal(X, X0) and np.array_equal(y, y0)):
        return "функции не должны изменять X и y"
    if w1.shape != (3,) or w2.shape != (3,) or w1.dtype.kind != "f" or w2.dtype.kind != "f":
        return "веса должны быть float-массивами формы (d + 1,) и для целочисленного входа"
    Xb = np.column_stack([np.ones(5), X.astype(float)])
    want = np.linalg.solve(Xb.T @ Xb, Xb.T @ y.astype(float))
    if not np.allclose(w1, want, rtol=0, atol=1e-8):
        return "для целочисленных данных lstsq должна дать то же решение, что и нормальные уравнения"


case("lstsq-vs-normal", t_lstsq_vs_normal)
case("ridge-basic", t_ridge_basic)
case("r2-score", t_r2)
case("rank-deficient", t_rank_deficient)
case("ridge-to-lstsq", t_ridge_to_lstsq)
case("r2-constant-target", t_r2_constant_target)
case("int-input-no-mutation", t_int_input_no_mutation)
