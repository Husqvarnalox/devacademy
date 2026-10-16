import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import whiten_task as m


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


def make_data(seed=0, n=200):
    rng = np.random.default_rng(seed)
    return rng.normal(size=(n, 3)) @ A + np.array([5.0, -3.0, 2.0])


def t_identity_covariance():
    X = make_data()
    Z, mean, comps, ev = m.whiten_fit_transform(X, 3)
    if Z.shape != (200, 3) or mean.shape != (3,) or comps.shape != (3, 3) or ev.shape != (3,):
        return f"неверные формы результата: {Z.shape}, {mean.shape}, {comps.shape}, {ev.shape}"
    if not np.allclose(Z.mean(axis=0), 0.0, rtol=0, atol=1e-10) or not np.allclose(np.cov(Z.T), np.eye(3), rtol=0, atol=1e-9):
        return "после отбеливания среднее нулевое, а выборочная ковариация (ddof=1) - единичная"


def t_partial_k():
    X = make_data(seed=1)
    Z, mean, comps, ev = m.whiten_fit_transform(X, 2)
    if Z.shape != (200, 2) or not np.allclose(np.cov(Z.T), np.eye(2), rtol=0, atol=1e-9):
        return "при k = 2 ожидались Z формы (200, 2) с единичной ковариацией"
    if not np.all(np.diff(ev) <= 0) or ev.shape != (2,):
        return "ev - дисперсии первых k компонент по убыванию"


def t_roundtrip():
    X = make_data(seed=2)
    Z, mean, comps, ev = m.whiten_fit_transform(X, 3)
    R = m.unwhiten(Z, mean, comps, ev)
    if R.shape != X.shape or not np.allclose(R, X, rtol=0, atol=1e-9):
        return "unwhiten(whiten(X)) при k = d должен вернуть X (допуск 1e-9)"


def t_rank_deficient():
    rng = np.random.default_rng(3)
    base = rng.normal(size=(60, 2))
    X = np.hstack([base, base[:, :1] - 2.0 * base[:, 1:]])
    import warnings
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        Z, mean, comps, ev = m.whiten_fit_transform(X, 3)
    if not np.isfinite(Z).all():
        return "на вырожденных данных Z не должна содержать nan/inf: у компонент с почти нулевой дисперсией делитель равен 1"
    if not np.allclose(np.cov(Z[:, :2].T), np.eye(2), rtol=0, atol=1e-9) or np.abs(Z[:, 2]).max() > 1e-6:
        return "две значимые компоненты отбеливаются до единичной дисперсии, третья остаётся около нуля"
    if not np.allclose(m.unwhiten(Z, mean, comps, ev), X, rtol=0, atol=1e-8):
        return "unwhiten должен восстановить X и в вырожденном случае"


def t_no_mutation_and_scale():
    X = make_data(seed=4)
    X0 = X.copy()
    Z1, *_ = m.whiten_fit_transform(X, 3)
    if not np.array_equal(X, X0):
        return "X не должен изменяться"
    Z2, *_ = m.whiten_fit_transform(X * 1000.0 + 7.0, 3)
    if not np.allclose(np.abs(Z1), np.abs(Z2), rtol=0, atol=1e-8):
        return "отбеливание инвариантно к масштабу и сдвигу данных (с точностью до знака компонент)"


case("identity-covariance", t_identity_covariance)
case("partial-k", t_partial_k)
case("roundtrip", t_roundtrip)
case("rank-deficient", t_rank_deficient)
case("no-mutation-and-scale", t_no_mutation_and_scale)
