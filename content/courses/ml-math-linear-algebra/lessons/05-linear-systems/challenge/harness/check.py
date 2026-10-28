import json
import math
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import conditioning_task as m


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


def t_solve_many_basic():
    A = np.array([[2.0, 1.0, 1.0], [1.0, 2.0, 0.0], [3.0, 0.0, 1.0]])
    B = np.array([[260.0, 2.0], [200.0, 1.0], [220.0, 3.0]])
    got = m.solve_many(A, B)
    if got.shape != (3, 2):
        return f"для A (3, 3) и B (3, 2) нужен результат (3, 2), получили {got.shape}"
    if not np.allclose(got[:, 0], [40.0, 80.0, 100.0], rtol=0, atol=1e-9):
        return f"первый столбец - цены из трёх чеков [40, 80, 100], получили {got[:, 0].tolist()}"
    if not np.allclose(A @ got, B, rtol=0, atol=1e-9):
        return "A @ X должна давать B"


def t_residual_basic():
    A = np.eye(2)
    got = m.relative_residual(A, np.array([1.0, 2.0]), np.array([3.0, 4.0]))
    want = math.sqrt(8.0) / 5.0
    if abs(got - want) > 1e-12:
        return f"||[-2, -2]|| / ||[3, 4]|| = {want:.8f}, получили {got!r}"
    got = m.relative_residual(A, np.array([3.0, 4.0]), np.array([3.0, 4.0]))
    if abs(got) > 1e-15:
        return f"точное решение даёт нулевую невязку, получили {got!r}"


def t_ratio_basic():
    A = np.diag([1.0, 10.0])
    b = np.array([10.0, 10.0])
    db = np.array([1.0, 0.0])
    got = m.perturbation_ratio(A, b, db)
    want = math.sqrt(200.0 / 101.0)
    if abs(got - want) > 1e-9:
        return f"x = [10, 1], dx = [1, 0]: отношение ошибок {want:.8f}, получили {got!r}"
    got = m.perturbation_ratio(np.eye(3), np.array([1.0, 2.0, 3.0]), np.array([0.1, -0.2, 0.3]))
    if abs(got - 1.0) > 1e-9:
        return f"для единичной матрицы ошибка не усиливается, отношение 1; получили {got!r}"


def t_solve_many_random():
    rng = np.random.default_rng(13)
    A = rng.normal(size=(5, 5)) + 5.0 * np.eye(5)
    B = rng.normal(size=(5, 4))
    X = m.solve_many(A, B)
    if X.shape != (5, 4) or not np.allclose(A @ X, B, rtol=0, atol=1e-9):
        return "A @ X должна совпадать с B на случайных данных"
    for j in range(4):
        if not np.allclose(X[:, j], np.linalg.solve(A, B[:, j]), rtol=0, atol=1e-10):
            return f"столбец {j} должен совпадать с решением системы для одной правой части"
    one = m.solve_many(A, B[:, :1])
    if one.shape != (5, 1):
        return f"при одной правой части B (5, 1) результат имеет форму (5, 1), получили {one.shape}"


def t_residual_edges():
    A = np.array([[1.0, 2.0], [3.0, 4.0]])
    got = m.relative_residual(A, np.array([1.0, 1.0]), np.zeros(2))
    if abs(got - math.sqrt(9.0 + 49.0)) > 1e-12:
        return f"при b = 0 верните ||A x|| = sqrt(58), получили {got!r}"
    got = m.relative_residual(A, np.zeros(2), np.zeros(2))
    if got != 0.0:
        return f"A x = 0 и b = 0: невязка 0, получили {got!r}"
    got = m.relative_residual(A, np.array([0.0, 0.0]), np.array([3.0, 4.0]))
    if abs(got - 1.0) > 1e-12:
        return f"нулевое решение даёт ||b|| / ||b|| = 1, получили {got!r}"
    got = m.relative_residual(A, np.array([1, 1]), np.array([3, 7]))
    if abs(got) > 1e-12 or not isinstance(got, float):
        return f"A [1, 1] = [3, 7]: невязка 0.0 (число float); получили {got!r}"


def t_ratio_bounds():
    rng = np.random.default_rng(99)
    for _ in range(25):
        n = int(rng.integers(2, 6))
        A = rng.normal(size=(n, n)) + 2.0 * np.eye(n)
        b = rng.normal(size=n)
        db = rng.normal(size=n) * 1e-6
        ratio = m.perturbation_ratio(A, b, db)
        c = np.linalg.cond(A)
        if not (1.0 / c * (1 - 1e-6) <= ratio <= c * (1 + 1e-6)):
            return f"отношение ошибок должно лежать между 1/cond и cond: {1.0 / c:.4g} <= {ratio:.4g} <= {c:.4g}"
    # масштаб db не должен менять отношение для линейной задачи
    A = np.array([[3.0, 1.0], [1.0, 2.0]])
    b = np.array([1.0, 1.0])
    db = np.array([0.3, -0.1])
    r1 = m.perturbation_ratio(A, b, db)
    r2 = m.perturbation_ratio(A, b, 1e-3 * db)
    if abs(r1 - r2) > 1e-6 * r1:
        return "отношение относительных ошибок не зависит от масштаба возмущения: решение линейно по b"


def t_ratio_ill():
    H = np.array([[1.0, 1.0], [1.0, 1.0001]])
    b = np.array([2.0, 2.0001])
    db = np.array([0.0, 0.00005])
    got = m.perturbation_ratio(H, b, db)
    if not (1e4 < got <= np.linalg.cond(H) * (1 + 1e-6)):
        return f"для почти вырожденной матрицы малое возмущение даёт большое отношение (~28 000), получили {got!r}"
    well = m.perturbation_ratio(np.array([[2.0, 0.0], [0.0, 1.0]]), b, db)
    if well > 2.0 + 1e-9:
        return "для хорошо обусловленной матрицы diag(2, 1) отношение не превышает cond = 2"


def t_errors_no_mutation():
    A = np.array([[1, 2], [3, 4]])
    B = np.array([[5, 6], [7, 8]])
    A0, B0 = A.copy(), B.copy()
    X = m.solve_many(A, B)
    m.relative_residual(A, X, B[:, 0])
    m.perturbation_ratio(A, np.array([1, 2]), np.array([0, 1]))
    if not (np.array_equal(A, A0) and np.array_equal(B, B0)):
        return "функции не должны менять входные массивы"
    if X.dtype.kind != "f" or not np.allclose(X, np.linalg.solve(A.astype(float), B.astype(float))):
        return "для целочисленного входа результат должен быть float и совпадать с np.linalg.solve"
    try:
        m.solve_many(np.array([[1.0, 2.0], [2.0, 4.0]]), np.array([[1.0], [2.0]]))
    except np.linalg.LinAlgError:
        pass
    else:
        return "для вырожденной матрицы solve_many должна выбросить LinAlgError (это даёт np.linalg.solve)"
    try:
        m.solve_many(np.eye(3), np.ones((2, 2)))
    except ValueError:
        pass
    else:
        return "число строк B не равно размеру A - ValueError"


case("solve-many-basic", t_solve_many_basic)
case("residual-basic", t_residual_basic)
case("ratio-basic", t_ratio_basic)
case("solve-many-random", t_solve_many_random)
case("residual-edges", t_residual_edges)
case("ratio-bounds", t_ratio_bounds)
case("ratio-ill-conditioned", t_ratio_ill)
case("errors-no-mutation", t_errors_no_mutation)
