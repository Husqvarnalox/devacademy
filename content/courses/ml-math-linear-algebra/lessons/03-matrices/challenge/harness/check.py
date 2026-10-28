import json
import math
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import layer_task as m


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


def raises_value_error(fn, *args):
    try:
        fn(*args)
    except ValueError:
        return True
    except Exception:
        return False
    return False


def loop_dense(X, W, b):
    out = []
    for x in X:
        out.append([sum(float(w) * float(v) for w, v in zip(row, x)) + float(bi) for row, bi in zip(W, b)])
    return np.array(out)


def t_batch_basic():
    W = np.array([[1.0, -1.0, 0.5], [0.0, 2.0, -1.0]])
    b = np.array([0.5, -1.0])
    X = np.array([[1.0, 2.0, 3.0], [4.0, 0.0, 1.0], [0.0, 0.0, 0.0]])
    got = m.batch_dense(X, W, b)
    want = np.array([[1.0, 0.0], [5.0, -2.0], [0.5, -1.0]])
    if got.shape != (3, 2):
        return f"для X (3, 3), W (2, 3) нужен результат (3, 2), получили {got.shape}"
    if not np.allclose(got, want, rtol=0, atol=1e-12):
        return f"ожидали {want.tolist()}, получили {got.tolist()}"


def t_rotation_basic():
    R = m.rotation_matrix(math.pi / 2)
    if R.shape != (2, 2):
        return f"матрица поворота имеет форму (2, 2), получили {R.shape}"
    if not np.allclose(R @ np.array([1.0, 0.0]), [0.0, 1.0], rtol=0, atol=1e-12):
        return "поворот на pi/2 переводит (1, 0) в (0, 1)"
    if not np.allclose(R @ np.array([0.0, 1.0]), [-1.0, 0.0], rtol=0, atol=1e-12):
        return "поворот на pi/2 переводит (0, 1) в (-1, 0)"
    R4 = m.rotation_matrix(math.pi / 4)
    s = math.sqrt(0.5)
    if not np.allclose(R4, [[s, -s], [s, s]], rtol=0, atol=1e-12):
        return "матрица поворота на pi/4: [[cos, -sin], [sin, cos]]"


def t_transform_basic():
    P = np.array([[1.0, 0.0], [0.0, 1.0], [2.0, 3.0]])
    shear = np.array([[1.0, 1.0], [0.0, 1.0]])
    got = m.transform_points(shear, P)
    want = np.array([[1.0, 0.0], [1.0, 1.0], [5.0, 3.0]])
    if got.shape != (3, 2) or not np.allclose(got, want, rtol=0, atol=1e-12):
        return f"сдвиг [[1, 1], [0, 1]]: (x, y) -> (x + y, y); ожидали {want.tolist()}, получили {got.tolist()}"
    got = m.transform_points(np.diag([2.0, -1.0]), P)
    if not np.allclose(got, [[2.0, 0.0], [0.0, -1.0], [4.0, -3.0]], rtol=0, atol=1e-12):
        return "матрица diag(2, -1) растягивает x вдвое и отражает y"


def t_batch_vs_loop():
    rng = np.random.default_rng(7)
    X = rng.normal(size=(6, 4))
    W = rng.normal(size=(3, 4))
    b = rng.normal(size=3)
    got = m.batch_dense(X, W, b)
    if not np.allclose(got, loop_dense(X, W, b), rtol=0, atol=1e-10):
        return "результат не совпал с расчётом циклом: для каждого объекта W x + b"
    one = m.batch_dense(X[:1], W, b)
    if one.shape != (1, 3) or not np.allclose(one[0], W @ X[0] + b, rtol=0, atol=1e-10):
        return "пакет из одного объекта должен давать (1, d_out) и совпадать с W x + b"
    wide = m.batch_dense(rng.normal(size=(2, 5)), rng.normal(size=(7, 5)), np.zeros(7))
    if wide.shape != (2, 7):
        return f"X (2, 5), W (7, 5): ожидали форму (2, 7), получили {wide.shape}"


def t_shape_errors():
    X = np.ones((4, 3))
    W = np.ones((2, 3))
    b = np.ones(2)
    if not raises_value_error(m.batch_dense, np.ones((4, 5)), W, b):
        return "число признаков X (5) не равно числу входов W (3): ValueError"
    if not raises_value_error(m.batch_dense, X, W, np.ones(3)):
        return "длина b (3) не равна числу нейронов W (2): ValueError"
    if not raises_value_error(m.batch_dense, X, np.ones(3), b):
        return "W должна быть двумерной: ValueError"
    if not raises_value_error(m.batch_dense, np.ones(3), W, b):
        return "X должна быть двумерной (n, d_in): ValueError"
    if not raises_value_error(m.batch_dense, X, W, np.ones((2, 1))):
        return "b должна быть одномерной: ValueError"
    if not raises_value_error(m.batch_dense, X, W.T, b):
        return "перепутанные оси W (3, 2) вместо (2, 3): ValueError"


def t_rotation_properties():
    rng = np.random.default_rng(11)
    P = rng.normal(size=(8, 2))
    for theta in (0.3, -1.1, 2.5, math.pi):
        R = m.rotation_matrix(theta)
        rotated = m.transform_points(R, P)
        if not np.allclose(np.linalg.norm(rotated, axis=1), np.linalg.norm(P, axis=1), rtol=0, atol=1e-12):
            return f"поворот на {theta} должен сохранять длины векторов"
        back = m.transform_points(m.rotation_matrix(-theta), rotated)
        if not np.allclose(back, P, rtol=0, atol=1e-12):
            return f"поворот на {theta}, а затем на -{theta} должен вернуть исходные точки"
        if not np.allclose(R @ R.T, np.eye(2), rtol=0, atol=1e-12) or abs(np.linalg.det(R) - 1.0) > 1e-12:
            return "матрица поворота ортогональна, её определитель равен 1"
    if not np.allclose(m.rotation_matrix(0.0), np.eye(2), rtol=0, atol=1e-15):
        return "поворот на 0 - единичная матрица"
    rotated = m.transform_points(m.rotation_matrix(math.pi / 2), np.array([[1.0, 0.0], [2.0, 2.0]]))
    if not np.allclose(rotated, [[0.0, 1.0], [-2.0, 2.0]], rtol=0, atol=1e-12):
        return "поворот на pi/2: (1, 0) -> (0, 1), (2, 2) -> (-2, 2) (строки точек, не столбцы)"


def t_int_empty_no_mutation():
    P = np.array([[1, 2], [3, 4]])
    A = np.array([[0, 1], [1, 0]])
    P0, A0 = P.copy(), A.copy()
    got = m.transform_points(A, P)
    if not (np.array_equal(P, P0) and np.array_equal(A, A0)):
        return "transform_points не должна менять входные массивы"
    if not np.allclose(got, [[2.0, 1.0], [4.0, 3.0]]):
        return f"отражение относительно y = x меняет координаты местами: [[2, 1], [4, 3]], получили {got.tolist()}"
    empty = m.transform_points(np.eye(2), np.zeros((0, 2)))
    if empty.shape != (0, 2):
        return f"для пустого набора точек форма результата (0, 2), получили {empty.shape}"
    X = np.array([[1, 2], [3, 4]])
    W = np.array([[1, 0], [0, 1], [1, 1]])
    b = np.array([1, 2, 3])
    X0, W0, b0 = X.copy(), W.copy(), b.copy()
    got = m.batch_dense(X, W, b)
    if not (np.array_equal(X, X0) and np.array_equal(W, W0) and np.array_equal(b, b0)):
        return "batch_dense не должна менять входные массивы"
    if not np.allclose(got, [[2.0, 4.0, 6.0], [4.0, 6.0, 10.0]]):
        return f"целочисленный вход: ожидали [[2, 4, 6], [4, 6, 10]], получили {got.tolist()}"


case("batch-basic", t_batch_basic)
case("rotation-basic", t_rotation_basic)
case("transform-basic", t_transform_basic)
case("batch-vs-loop", t_batch_vs_loop)
case("shape-errors", t_shape_errors)
case("rotation-properties", t_rotation_properties)
case("int-empty-no-mutation", t_int_empty_no_mutation)
