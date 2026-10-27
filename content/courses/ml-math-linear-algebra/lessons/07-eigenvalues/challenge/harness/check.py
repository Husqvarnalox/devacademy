import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import eigh_task as m


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


def make_X(seed=4, n=200):
    rng = np.random.default_rng(seed)
    return rng.normal(size=(n, 3)) @ np.array([[2.0, 0.5, 0.0], [0.0, 1.0, 0.3], [0.0, 0.0, 0.4]]) + np.array([5.0, -3.0, 1.0])


def power_direction(S, iters=2000):
    v = np.ones(len(S))
    for _ in range(iters):
        v = S @ v
        v /= np.linalg.norm(v)
    return v


def t_covariance():
    X = make_X()
    got = m.covariance_matrix(X)
    want = np.cov(X.T)
    if not isinstance(got, np.ndarray) or got.shape != (3, 3):
        return f"ожидался массив формы (3, 3), получили {type(got).__name__} {getattr(got, 'shape', None)}"
    if not np.allclose(got, want, rtol=1e-10, atol=1e-10):
        return "ковариация должна совпасть с np.cov(X.T): Xc^T Xc / (n - 1), где Xc - центрированные данные"
    small = np.array([[1.0, 2.0], [3.0, 2.0], [2.0, 5.0]])
    if not np.allclose(m.covariance_matrix(small), [[1.0, 0.0], [0.0, 3.0]], atol=1e-12):
        return "для точек [1, 2], [3, 2], [2, 5] ковариация равна [[1, 0], [0, 3]]"


def t_eigh_properties():
    S = np.cov(make_X(seed=9).T)
    vals, vecs = m.sorted_eigh(S)
    if vals.shape != (3,) or vecs.shape != (3, 3):
        return f"ожидались формы (3,) и (3, 3), получили {vals.shape} и {vecs.shape}"
    if not np.all(np.diff(vals) <= 1e-12):
        return f"собственные значения должны идти по убыванию, получили {vals.tolist()}"
    if not np.allclose(S @ vecs, vecs * vals, atol=1e-9):
        return "каждый столбец vecs должен быть собственным вектором: S v = lam v"
    if not np.allclose(vecs.T @ vecs, np.eye(3), atol=1e-9):
        return "собственные векторы симметричной матрицы ортонормированы: V^T V = I"


def t_reconstruct():
    S = np.cov(make_X(seed=1).T)
    vals, vecs = m.sorted_eigh(S)
    back = m.reconstruct(vals, vecs)
    if not np.allclose(back, S, atol=1e-9):
        return "V diag(vals) V^T должно восстанавливать исходную матрицу"
    got = m.reconstruct(np.array([3.0, 1.0]), np.eye(2))
    if not np.allclose(got, np.diag([3.0, 1.0])):
        return "для V = I результат - diag(vals)"


def t_known_matrix():
    vals, vecs = m.sorted_eigh(np.array([[2.0, 1.0], [1.0, 2.0]]))
    if not np.allclose(vals, [3.0, 1.0], atol=1e-12):
        return f"собственные значения [[2, 1], [1, 2]] - 3 и 1 (по убыванию), получили {vals.tolist()}"
    s = 1 / np.sqrt(2)
    top, second = vecs[:, 0], vecs[:, 1]
    if not (np.allclose(np.abs(top), [s, s], atol=1e-9) and top[0] * top[1] > 0):
        return "вектор для lam = 3 пропорционален (1, 1)"
    if not (np.allclose(np.abs(second), [s, s], atol=1e-9) and second[0] * second[1] < 0):
        return "вектор для lam = 1 пропорционален (1, -1)"


def t_power_direction():
    S = np.cov(make_X(seed=6).T)
    vals, vecs = m.sorted_eigh(S)
    v = power_direction(S)
    top = vecs[:, 0]
    if not abs(abs(float(v @ top)) - 1.0) <= 1e-8:
        return "первый столбец vecs должен совпадать (с точностью до знака) с направлением степенного метода"
    if not abs(float(v @ S @ v) - vals[0]) <= 1e-8:
        return "наибольшее собственное значение равно отношению Рэлея для главного вектора"


def t_int_input_no_mutation():
    X = np.array([[1, 2], [3, 2], [2, 5], [4, 4]])
    S = np.array([[4, 1], [1, 3]])
    X0, S0 = X.copy(), S.copy()
    C = m.covariance_matrix(X)
    vals, vecs = m.sorted_eigh(S)
    m.reconstruct(vals, vecs)
    if not (np.array_equal(X, X0) and np.array_equal(S, S0)):
        return "функции не должны изменять входные массивы"
    if C.dtype.kind != "f" or not np.allclose(C, np.cov(X.T)):
        return "для целочисленных данных ковариация должна быть float и совпадать с np.cov"
    if not np.allclose(m.reconstruct(vals, vecs), S, atol=1e-9):
        return "reconstruct(sorted_eigh(S)) должна восстановить целочисленную S"
    if not np.allclose(C, C.T):
        return "ковариационная матрица симметрична"


def t_repeated_values():
    S = 2.0 * np.eye(3)
    vals, vecs = m.sorted_eigh(S)
    if not np.allclose(vals, [2.0, 2.0, 2.0]):
        return "у 2*I все три собственных значения равны 2"
    if not np.allclose(vecs.T @ vecs, np.eye(3), atol=1e-12):
        return "при кратном значении векторы всё равно должны быть ортонормированы"
    D = np.diag([1.0, 5.0, 3.0])
    vals, vecs = m.sorted_eigh(D)
    if not np.allclose(vals, [5.0, 3.0, 1.0]) or not np.allclose(np.abs(vecs[:, 0]), [0.0, 1.0, 0.0]):
        return "для diag(1, 5, 3) значения 5, 3, 1; первый вектор - ось 2"
    if abs(np.trace(m.reconstruct(vals, vecs)) - 9.0) > 1e-9:
        return "след матрицы равен сумме собственных значений"


case("covariance", t_covariance)
case("eigh-properties", t_eigh_properties)
case("reconstruct", t_reconstruct)
case("known-matrix", t_known_matrix)
case("power-direction", t_power_direction)
case("int-input-no-mutation", t_int_input_no_mutation)
case("repeated-values", t_repeated_values)
