import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import linalg_task as m


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


def t_matmul_shape_basic():
    cases = [((3, 4), (4, 5), (3, 5)), ((4,), (4,), ()), ((4,), (4, 2), (2,)), ((3, 4), (4,), (3,))]
    for s1, s2, want in cases:
        got = m.matmul_shape(s1, s2)
        if tuple(got) != want:
            return f"matmul_shape({s1}, {s2}): ожидалось {want}, получили {got}"


def t_affine_basic():
    X = np.array([[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]])
    W = np.array([[1.0, 0.0], [0.0, 1.0], [1.0, 1.0]])
    b = np.array([10.0, 20.0])
    out = m.affine(X, W, b)
    if out.shape != (2, 2) or out.dtype != np.float64 or not np.allclose(out, [[14.0, 25.0], [20.0, 31.0]], rtol=0, atol=1e-12):
        return f"ожидалось [[14, 25], [20, 31]], получили {out!r}"


def t_solve_basic():
    A = np.array([[3.0, 1.0], [1.0, 2.0]])
    b = np.array([9.0, 8.0])
    x = m.solve_system(A, b)
    if x.shape != (2,) or not np.allclose(x, [2.0, 3.0], rtol=1e-12, atol=1e-12):
        return f"ожидалось [2, 3], получили {x!r}"


def t_row_norms():
    a = np.array([[3.0, 4.0], [-1.0, 1.0], [0.0, -2.0]])
    for o, want in ((2, [5.0, 2 ** 0.5, 2.0]), (1, [7.0, 2.0, 2.0]), (np.inf, [4.0, 1.0, 2.0])):
        got = m.row_norms(a, o)
        if got.shape != (3,) or not np.allclose(got, want, rtol=1e-12, atol=1e-12):
            return f"row_norms(a, {o}): ожидалось {want}, получили {got!r}"


def t_matmul_shape_errors():
    for s1, s2 in (((3, 4), (3, 4)), ((4,), (3,)), ((2, 3), (4,)), ((5,), (4, 2))):
        try:
            got = m.matmul_shape(s1, s2)
        except ValueError:
            continue
        return f"matmul_shape({s1}, {s2}) должна бросать ValueError, а вернула {got}"


def t_square_matmul_vs_star():
    X = np.array([[1.0, 2.0, 0.0], [0.0, 1.0, 3.0], [4.0, 0.0, 1.0]])
    W = np.array([[2.0, 0.0, 1.0], [1.0, 1.0, 0.0], [0.0, 5.0, 1.0]])
    b = np.array([1.0, -1.0, 0.5])
    out = m.affine(X, W, b)
    want = X @ W + b
    if not np.allclose(out, want, rtol=0, atol=1e-12):
        wrong = X * W + b
        hint = " (похоже, вы использовали поэлементное *)" if np.allclose(out, wrong, rtol=0, atol=1e-12) else ""
        return f"affine на квадратных X и W: ожидалось {want.tolist()}, получили {out.tolist()}{hint}"
    ints = m.affine(np.array([[1, 2]]), np.array([[1], [1]]), np.array([0]))
    if ints.dtype != np.float64 or ints.shape != (1, 1) or ints[0, 0] != 3.0:
        return f"целые входы: ожидался float64 [[3.]], получили {ints!r} {ints.dtype}"


def t_solve_not_inv():
    real_inv = np.linalg.inv
    real_pinv = np.linalg.pinv

    def forbidden(*_a, **_k):
        raise AssertionError("используйте np.linalg.solve: явное обращение матрицы медленнее и менее точно")

    rng = np.random.default_rng(21)
    A = rng.normal(size=(30, 30)) + 6.0 * np.eye(30)
    x_true = rng.normal(size=30)
    b = A @ x_true
    B = rng.normal(size=(30, 4))
    np.linalg.inv, np.linalg.pinv = forbidden, forbidden
    try:
        x = m.solve_system(A, b)
        X = m.solve_system(A, B)
    finally:
        np.linalg.inv, np.linalg.pinv = real_inv, real_pinv
    if x.shape != (30,) or not np.allclose(x, x_true, rtol=1e-9, atol=1e-9):
        return "решение не совпало с эталоном (допуск 1e-9)"
    if X.shape != (30, 4) or not np.allclose(A @ X, B, rtol=0, atol=1e-9):
        return "для правой части-матрицы (30, 4) должно выполняться A @ X = B (допуск 1e-9)"


def t_solve_singular_and_hilbert():
    try:
        m.solve_system(np.array([[1.0, 2.0], [2.0, 4.0]]), np.array([1.0, 2.0]))
    except np.linalg.LinAlgError:
        pass
    else:
        return "для вырожденной матрицы нужно пробросить np.linalg.LinAlgError"
    n = 10
    H = 1.0 / (np.arange(1, n + 1)[:, None] + np.arange(n)[None, :])
    b = H @ np.ones(n)
    x = m.solve_system(H, b)
    res = np.linalg.norm(H @ x - b)
    if not res < 1e-12:
        return f"на матрице Гильберта остаток ||Ax - b|| должен быть меньше 1e-12 (solve обратно устойчив), получили {res:.2e}"


def t_normalize_rows():
    a = np.array([[3.0, 4.0, 0.0], [0.0, 0.0, 0.0], [1.0, 1.0, 1.0]])
    out = m.normalize_rows(a)
    want = np.array([[0.6, 0.8, 0.0], [0.0, 0.0, 0.0], [3 ** -0.5] * 3])
    if out.shape != a.shape or out.dtype != np.float64 or not np.allclose(out, want, rtol=0, atol=1e-12):
        return f"ожидалось {want.tolist()}, получили {out!r}"
    sq = np.array([[1.0, 2.0], [3.0, 4.0]])
    o = m.normalize_rows(sq)
    if not np.allclose(np.linalg.norm(o, axis=1), 1.0, rtol=0, atol=1e-12):
        return "нормы строк результата должны быть равны 1 (на квадратной матрице проверьте keepdims)"
    if not np.isfinite(out).all():
        return "в результате не должно быть nan/inf"


case("matmul-shape-basic", t_matmul_shape_basic)
case("affine-basic", t_affine_basic)
case("solve-basic", t_solve_basic)
case("row-norms", t_row_norms)
case("matmul-shape-errors", t_matmul_shape_errors)
case("square-matmul-vs-star", t_square_matmul_vs_star)
case("solve-not-inv", t_solve_not_inv)
case("solve-singular-and-hilbert", t_solve_singular_and_hilbert)
case("normalize-rows", t_normalize_rows)
