import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import broadcast_task as m


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

BAD_PAIRS = [((3,), (4,)), ((2, 3), (3, 2)), ((5, 1, 4), (3, 2)), ((2,), (3,))]
GOOD_PAIRS = [((3,), (3,)), ((3, 4), (4,)), ((3, 1), (1, 4)), ((), (5,)), ((2, 1, 4), (3, 1)), ((1,), (7, 1)), ((5, 1), (1,)), ((8, 1, 6, 1), (7, 1, 5))]


def t_shape_good_basic():
    for s1, s2 in GOOD_PAIRS[:4]:
        got = m.broadcast_shape(s1, s2)
        want = np.broadcast_shapes(s1, s2)
        if tuple(got) != want:
            return f"broadcast_shape({s1}, {s2}): ожидалось {want}, получили {got}"


def t_center_columns():
    a = np.array([[1.0, 10.0, 100.0], [3.0, 20.0, 200.0], [5.0, 60.0, 300.0]])
    out = m.center_columns(a)
    want = a - a.mean(axis=0)
    if out.shape != (3, 3) or out.dtype != np.float64 or not np.allclose(out, want, rtol=0, atol=1e-12):
        return f"ожидалось {want.tolist()}, получили {out!r}"
    if not np.allclose(out.mean(axis=0), 0, rtol=0, atol=1e-12):
        return "среднее каждого столбца после центрирования должно быть 0"


def t_row_normalize_rect():
    a = np.array([[1.0, 3.0, 4.0, 2.0], [0.0, 5.0, 5.0, 0.0], [2.0, 2.0, 2.0, 4.0]])
    out = m.row_normalize(a)
    want = a / a.sum(axis=1, keepdims=True)
    if out.shape != a.shape or not np.allclose(out, want, rtol=0, atol=1e-12):
        return f"ожидалось {want.tolist()}, получили {out!r}"


def t_outer_sum():
    out = m.outer_sum(np.array([1, 2, 3]), np.array([10, 20]))
    if out.shape != (3, 2) or out.tolist() != [[11, 21], [12, 22], [13, 23]]:
        return f"ожидалось [[11, 21], [12, 22], [13, 23]] формы (3, 2), получили {out!r}"


def t_shape_errors():
    for s1, s2 in BAD_PAIRS:
        try:
            got = m.broadcast_shape(s1, s2)
        except ValueError:
            continue
        return f"broadcast_shape({s1}, {s2}) должна бросать ValueError, а вернула {got}"


def t_shape_hard():
    for s1, s2 in GOOD_PAIRS:
        got = m.broadcast_shape(s1, s2)
        want = np.broadcast_shapes(s1, s2)
        if tuple(got) != want or not all(type(x) is int for x in got):
            return f"broadcast_shape({s1}, {s2}): ожидалось {want}, получили {got}"
        if tuple(m.broadcast_shape(s2, s1)) != want:
            return f"результат должен быть симметричным: ({s2}, {s1})"


def t_square_matrix_trap():
    """На квадратной несимметричной матрице неверная ось не вызывает ошибку - даёт тихо неправильный ответ."""
    a = np.array([[1.0, 2.0, 6.0], [4.0, 8.0, 0.0], [9.0, 3.0, 3.0]])
    c = m.center_columns(a)
    if not np.allclose(c, a - a.mean(axis=0), rtol=0, atol=1e-12):
        return "center_columns: на квадратной матрице вы центрируете по строкам вместо столбцов (нужен axis=0)"
    r = m.row_normalize(a)
    want = a / a.sum(axis=1, keepdims=True)
    if not np.allclose(r, want, rtol=0, atol=1e-12):
        return ("row_normalize: на квадратной матрице a / a.sum(axis=1) делит столбцы, а не строки "
                "(форма (3,) выравнивается по ПОСЛЕДНЕЙ оси); нужен keepdims=True")
    if not np.allclose(r.sum(axis=1), 1.0, rtol=0, atol=1e-12):
        return "сумма каждой строки после нормировки должна быть 1"


def t_row_normalize_zero_and_ints():
    a = np.array([[0, 0, 0], [1, 1, 2], [3, 0, 1]])
    out = m.row_normalize(a)
    want = np.array([[0.0, 0.0, 0.0], [0.25, 0.25, 0.5], [0.75, 0.0, 0.25]])
    if out.dtype != np.float64 or not np.allclose(out, want, rtol=0, atol=1e-12):
        return f"целый вход и нулевая строка: ожидалось {want.tolist()}, получили {out!r} {out.dtype}"
    if not np.isfinite(out).all():
        return "в результате не должно быть nan/inf"


def t_outer_sum_shapes():
    out = m.outer_sum(np.arange(1, 5.0), np.array([0.5]))
    if out.shape != (4, 1):
        return f"форма должна быть (n, m) = (4, 1), получили {out.shape}"
    sq = m.outer_sum(np.array([1, 2]), np.array([10, 100]))
    if sq.tolist() != [[11, 101], [12, 102]]:
        return f"на квадратной форме проверьте порядок осей: x вдоль строк, y вдоль столбцов; получили {sq.tolist()}"
    x = np.arange(3)
    if m.outer_sum(x, x).tolist() != [[0, 1, 2], [1, 2, 3], [2, 3, 4]]:
        return "outer_sum(x, x) должно быть x[i] + x[j]"


case("shape-good-basic", t_shape_good_basic)
case("center-columns", t_center_columns)
case("row-normalize-rect", t_row_normalize_rect)
case("outer-sum", t_outer_sum)
case("shape-errors", t_shape_errors)
case("shape-hard", t_shape_hard)
case("square-matrix-trap", t_square_matrix_trap)
case("row-normalize-zero-and-ints", t_row_normalize_zero_and_ints)
case("outer-sum-shapes", t_outer_sum_shapes)
