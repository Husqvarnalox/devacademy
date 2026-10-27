import json
import math
import os
import warnings

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import search_task as m


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


def loop_cosine(a, b):
    na = math.sqrt(sum(float(x) * float(x) for x in a))
    nb = math.sqrt(sum(float(x) * float(x) for x in b))
    if na == 0.0 or nb == 0.0:
        return 0.0
    return sum(float(x) * float(y) for x, y in zip(a, b)) / (na * nb)


def t_normalize_basic():
    X = np.array([[3.0, 4.0], [0.0, -2.0], [1.0, 1.0]])
    got = m.normalize_rows(X)
    want = np.array([[0.6, 0.8], [0.0, -1.0], [math.sqrt(0.5), math.sqrt(0.5)]])
    if not isinstance(got, np.ndarray) or got.shape != (3, 2):
        return "normalize_rows должна вернуть массив формы (3, 2)"
    if not np.allclose(got, want, rtol=0, atol=1e-12):
        return f"строки [3, 4], [0, -2], [1, 1] после нормализации: {want.tolist()}, получили {got.tolist()}"
    if not np.allclose(np.linalg.norm(got, axis=1), 1.0, rtol=0, atol=1e-12):
        return "длина каждой нормализованной строки должна быть равна 1"


def t_cosine_matrix_basic():
    A = np.array([[1.0, 0.0], [1.0, 1.0]])
    B = np.array([[2.0, 0.0], [0.0, 3.0], [-1.0, -1.0]])
    got = m.cosine_similarity_matrix(A, B)
    s = math.sqrt(0.5)
    want = np.array([[1.0, 0.0, -s], [s, s, -1.0]])
    if got.shape != (2, 3):
        return f"для A (2, d) и B (3, d) нужна матрица (2, 3), получили {got.shape}"
    if not np.allclose(got, want, rtol=0, atol=1e-12):
        return f"ожидали {want.tolist()}, получили {got.tolist()}"


def t_nearest_basic():
    X = np.array([[0.0, 1.0], [1.0, 0.1], [1.0, 1.0], [-1.0, 0.0]])
    Q = np.array([[1.0, 0.0], [-1.0, 0.1]])
    got = m.nearest(Q, X, 2)
    want = np.array([[1, 2], [3, 0]])
    if got.shape != (2, 2) or got.dtype.kind not in "iu":
        return f"nearest должна вернуть целочисленный массив (2, 2), получили shape={got.shape} dtype={got.dtype}"
    if not np.array_equal(got, want):
        return f"ожидали {want.tolist()}, получили {got.tolist()}"


def t_zero_rows():
    X = np.array([[0.0, 0.0], [3.0, 4.0]])
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        got = m.normalize_rows(X)
        S = m.cosine_similarity_matrix(X, np.array([[1.0, 0.0], [0.0, 0.0]]))
    if not np.all(np.isfinite(got)) or not np.array_equal(got[0], [0.0, 0.0]):
        return "нулевая строка должна остаться нулевой: без nan и inf"
    if not np.all(np.isfinite(S)):
        return "косинусы с нулевыми строками должны быть конечными числами (0), без nan"
    if not np.allclose(S, [[0.0, 0.0], [0.6, 0.0]], rtol=0, atol=1e-12):
        return f"косинусы нулевой строки равны 0; ожидали [[0, 0], [0.6, 0]], получили {S.tolist()}"


def t_matrix_vs_loop():
    rng = np.random.default_rng(42)
    A = rng.normal(size=(7, 5))
    B = rng.normal(size=(9, 5))
    A[3] = 0.0
    got = m.cosine_similarity_matrix(A, B)
    want = np.array([[loop_cosine(a, b) for b in B] for a in A])
    if got.shape != (7, 9) or not np.allclose(got, want, rtol=0, atol=1e-10):
        return "матрица косинусов не совпала с попарным расчётом циклом"
    idx = m.nearest(A, B, 4)
    want_idx = np.array([sorted(range(9), key=lambda j: (-want[i, j], j))[:4] for i in range(7)])
    if not np.array_equal(idx, want_idx):
        return "nearest не совпала с сортировкой по убыванию косинуса из цикла"


def t_nearest_ties():
    X = np.array([[1.0, 0.0], [2.0, 0.0], [0.0, 1.0], [3.0, 0.0], [5.0, 0.0]])
    Q = np.array([[1.0, 0.0]])
    got = m.nearest(Q, X, 5)
    if got.tolist() != [[0, 1, 3, 4, 2]]:
        return f"при равных косинусах выигрывает меньший индекс: [[0, 1, 3, 4, 2]], получили {got.tolist()}"
    one = m.nearest(Q, X, 1)
    if one.shape != (1, 1) or int(one[0, 0]) != 0:
        return f"k = 1 даёт форму (1, 1) с индексом 0, получили {one.tolist()}"


def t_int_no_mutation():
    X = np.array([[3, 4], [0, 0]])
    Q = np.array([[3, 4]])
    X0, Q0 = X.copy(), Q.copy()
    got = m.normalize_rows(X)
    m.cosine_similarity_matrix(Q, X)
    m.nearest(Q, X, 1)
    if not (np.array_equal(X, X0) and np.array_equal(Q, Q0)):
        return "входные массивы не должны изменяться"
    if got.dtype.kind != "f" or not np.allclose(got, [[0.6, 0.8], [0.0, 0.0]], rtol=0, atol=1e-12):
        return f"для целочисленного входа нужен результат float: [[0.6, 0.8], [0, 0]], получили {got.tolist()} dtype={got.dtype}"


case("normalize-basic", t_normalize_basic)
case("cosine-matrix-basic", t_cosine_matrix_basic)
case("nearest-basic", t_nearest_basic)
case("zero-rows", t_zero_rows)
case("matrix-vs-loop", t_matrix_vs_loop)
case("nearest-ties", t_nearest_ties)
case("int-no-mutation", t_int_no_mutation)
