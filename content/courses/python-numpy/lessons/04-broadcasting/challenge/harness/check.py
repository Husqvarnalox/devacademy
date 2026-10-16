import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import distance_task as m


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


def ref(A, B):
    n, m_ = len(A), len(B)
    out = np.zeros((n, m_))
    for i in range(n):
        for j in range(m_):
            out[i, j] = sum((A[i, k] - B[j, k]) ** 2 for k in range(A.shape[1]))
    return out


def t_small():
    A = np.array([[0.0, 0.0], [1.0, 1.0]])
    B = np.array([[1.0, 0.0], [0.0, 2.0], [3.0, 3.0]])
    D = m.pairwise_sq_dists(A, B)
    if D.shape != (2, 3) or D.dtype != np.float64 or not np.allclose(D, [[1, 4, 18], [1, 2, 8]], rtol=0, atol=1e-12):
        return f"ожидалось [[1, 4, 18], [1, 2, 8]] формы (2, 3), получили {D!r}"


def t_nearest_basic():
    A = np.array([[0.1, 0.0], [5.0, 5.0]])
    B = np.array([[0.0, 0.0], [10.0, 10.0], [5.0, 4.0]])
    idx = m.nearest_index(A, B)
    if idx.shape != (2,) or not np.issubdtype(idx.dtype, np.integer) or idx.tolist() != [0, 2]:
        return f"ожидалось [0, 2], получили {idx!r} {idx.dtype}"


def t_random_against_loops():
    rng = np.random.default_rng(12)
    A = rng.normal(size=(7, 4))
    B = rng.normal(size=(5, 4))
    D = m.pairwise_sq_dists(A, B)
    if D.shape != (7, 5) or not np.allclose(D, ref(A, B), rtol=1e-9, atol=1e-10):
        return "значения не совпали с эталоном на циклах (допуск rtol=1e-9, atol=1e-10)"
    if (D < 0).any():
        return "квадраты расстояний не могут быть отрицательными"


def t_no_mutation_and_dtypes():
    A = np.arange(6, dtype=np.int32).reshape(3, 2)
    B = np.arange(4, dtype=np.int32).reshape(2, 2)
    a0, b0 = A.copy(), B.copy()
    D = m.pairwise_sq_dists(A, B)
    if not (np.array_equal(A, a0) and np.array_equal(B, b0) and A.dtype == np.int32):
        return "входные массивы изменились"
    if D.dtype != np.float64 or not np.allclose(D, ref(A, B), rtol=0, atol=1e-12):
        return f"для int32-входа ожидался float64 с верными значениями, получили {D!r} {D.dtype}"


def t_one_dim_and_ties():
    A = np.array([[0.0], [2.0]])
    B = np.array([[1.0], [3.0], [1.0]])
    D = m.pairwise_sq_dists(A, B)
    if D.shape != (2, 3) or not np.allclose(D, [[1, 9, 1], [1, 1, 1]], rtol=0, atol=1e-12):
        return f"d=1: получили {D!r}"
    idx = m.nearest_index(A, B)
    if idx.tolist() != [0, 0]:
        return f"при равенстве расстояний нужен наименьший индекс: ожидалось [0, 0], получили {idx.tolist()}"


def t_no_cancellation():
    """Большое смещение: формула |a|^2 - 2ab + |b|^2 теряет точность, разности через broadcasting - нет."""
    A = np.array([[1e8, 1e8], [1e8 + 1, 1e8]])
    B = np.array([[1e8, 1e8 + 1]])
    D = m.pairwise_sq_dists(A, B)
    if not np.allclose(D, [[1.0], [2.0]], rtol=0, atol=1e-6):
        return f"при больших координатах ожидалось [[1], [2]] (допуск 1e-6), получили {D.ravel()}; считайте разности (A[:, None] - B[None]) до возведения в квадрат"


case("small", t_small)
case("nearest-basic", t_nearest_basic)
case("random-against-loops", t_random_against_loops)
case("no-mutation-and-dtypes", t_no_mutation_and_dtypes)
case("one-dim-and-ties", t_one_dim_and_ties)
case("no-cancellation", t_no_cancellation)
