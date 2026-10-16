import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import pca_task as m


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

A = np.array([[3.0, 0.0, 0.0], [1.0, 1.5, 0.0], [0.5, 0.2, 0.3]])
OFFSET = np.array([5.0, -3.0, 2.0])


def make_data(seed=0, n=200, offset=OFFSET):
    rng = np.random.default_rng(seed)
    return rng.normal(size=(n, 3)) @ A + offset


def eig_ref(X):
    vals, vecs = np.linalg.eigh(np.cov(X.T))
    order = np.argsort(vals)[::-1]
    return vals[order], vecs[:, order].T          # components in rows


def t_shapes():
    X = make_data()
    mean, comps, ev, evr = m.pca_fit(X, 2)
    if mean.shape != (3,) or comps.shape != (2, 3) or ev.shape != (2,) or evr.shape != (2,):
        return f"ожидались формы (3,), (2, 3), (2,), (2,); получили {mean.shape}, {comps.shape}, {ev.shape}, {evr.shape}"
    Z = m.pca_transform(X, mean, comps)
    R = m.pca_reconstruct(Z, mean, comps)
    if Z.shape != (200, 2) or R.shape != (200, 3):
        return f"transform должна вернуть (200, 2), reconstruct - (200, 3); получили {Z.shape}, {R.shape}"


def t_mean_and_orthonormal():
    X = make_data()
    mean, comps, ev, evr = m.pca_fit(X, 3)
    if not np.allclose(mean, X.mean(axis=0), rtol=0, atol=1e-12):
        return "mean должен быть средним по столбцам X"
    if not np.allclose(comps @ comps.T, np.eye(3), rtol=0, atol=1e-10):
        return "строки components должны быть ортонормированными"


def t_explained_variance_matches_eigh():
    X = make_data()
    mean, comps, ev, evr = m.pca_fit(X, 3)
    vals, _ = eig_ref(X)
    if not np.allclose(ev, vals, rtol=1e-8, atol=1e-10):
        return f"explained_variance = s^2 / (n - 1) должна совпасть с собственными числами ковариации (ddof=1): {ev!r} против {vals!r}"
    if not np.all(np.diff(ev) <= 0):
        return "компоненты должны идти по убыванию дисперсии"


def t_components_up_to_sign():
    X = make_data(seed=1)
    mean, comps, ev, evr = m.pca_fit(X, 3)
    _, ref = eig_ref(X)
    dots = np.abs(np.sum(comps * ref, axis=1))
    if not np.allclose(dots, 1.0, rtol=0, atol=1e-8):
        return f"компоненты должны совпасть с собственными векторами ковариации с точностью до знака; |cos| = {dots!r}"


def t_ratio_sums_to_one():
    X = make_data(seed=2)
    _, _, ev, evr = m.pca_fit(X, 3)
    if not np.isclose(evr.sum(), 1.0, rtol=0, atol=1e-12):
        return f"при k = d доли дисперсии должны давать 1, получили {evr.sum()!r}"
    _, _, ev2, evr2 = m.pca_fit(X, 2)
    if not np.allclose(evr2, evr[:2], rtol=0, atol=1e-12):
        return "доли считаются от полной дисперсии всех d компонент, а не только от k выбранных"


def t_centering_matters():
    X = make_data(seed=3, offset=np.array([1000.0, -1000.0, 500.0]))
    mean, comps, ev, evr = m.pca_fit(X, 3)
    vals, ref = eig_ref(X)
    if not np.allclose(mean, X.mean(axis=0), rtol=0, atol=1e-9):
        return "большое смещение данных: mean должен быть средним"
    if not np.allclose(ev, vals, rtol=1e-6, atol=1e-8) or not np.allclose(np.abs(np.sum(comps * ref, axis=1)), 1.0, atol=1e-6):
        return "без центрирования первая компонента указывала бы на среднее; вычтите mean перед SVD"


def t_transform_uncorrelated():
    X = make_data(seed=4)
    mean, comps, ev, evr = m.pca_fit(X, 3)
    Z = m.pca_transform(X, mean, comps)
    if not np.allclose(Z.mean(axis=0), 0.0, rtol=0, atol=1e-10):
        return "координаты центрированных данных должны иметь нулевое среднее"
    C = np.cov(Z.T)
    if not np.allclose(C, np.diag(ev), rtol=0, atol=1e-8):
        return "ковариация координат должна быть диагональной с explained_variance на диагонали"


def t_reconstruction_error():
    X = make_data(seed=5)
    n = len(X)
    vals, _ = eig_ref(X)
    e3 = m.reconstruction_error(X, 3)
    if not isinstance(e3, float) or e3 > 1e-18 * 1e6 + 1e-9:
        return f"при k = d ошибка реконструкции должна быть близка к 0, получили {e3!r}"
    e1 = m.reconstruction_error(X, 1)
    want1 = vals[1:].sum() * (n - 1) / n
    e2 = m.reconstruction_error(X, 2)
    want2 = vals[2] * (n - 1) / n
    if not (np.isclose(e1, want1, rtol=1e-8, atol=0) and np.isclose(e2, want2, rtol=1e-8, atol=0)):
        return f"ошибка равна сумме отброшенных собственных чисел * (n - 1) / n: ожидалось {want1:.6f}, {want2:.6f}; получили {e1:.6f}, {e2:.6f}"
    if not e3 <= e2 <= e1:
        return "ошибка реконструкции должна убывать с ростом k"


def t_rank_deficient_and_no_mutation():
    rng = np.random.default_rng(6)
    base = rng.normal(size=(50, 2))
    X = np.hstack([base, base[:, :1] + base[:, 1:]])        # третий признак - сумма первых двух
    X0 = X.copy()
    mean, comps, ev, evr = m.pca_fit(X, 3)
    if not np.array_equal(X, X0):
        return "pca_fit не должна изменять X на месте"
    if not (np.isfinite(ev).all() and ev[2] < 1e-10 * ev[0] and abs(evr[:2].sum() - 1.0) < 1e-10):
        return f"при вырожденных признаках третья дисперсия ~ 0, а две первые дают всю дисперсию: {ev!r}"
    Z = m.pca_transform(X, mean, comps[:2])
    R = m.pca_reconstruct(Z, mean, comps[:2])
    if not np.allclose(R, X, rtol=0, atol=1e-9):
        return "двух компонент достаточно для точного восстановления данных ранга 2"


case("shapes", t_shapes)
case("mean-and-orthonormal", t_mean_and_orthonormal)
case("explained-variance-matches-eigh", t_explained_variance_matches_eigh)
case("components-up-to-sign", t_components_up_to_sign)
case("ratio-sums-to-one", t_ratio_sums_to_one)
case("centering-matters", t_centering_matters)
case("transform-uncorrelated", t_transform_uncorrelated)
case("reconstruction-error", t_reconstruction_error)
case("rank-deficient-no-mutation", t_rank_deficient_and_no_mutation)
