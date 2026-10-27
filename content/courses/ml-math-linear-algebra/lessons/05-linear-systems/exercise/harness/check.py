import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import gauss_task as m


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


def close(a, b, tol=1e-9):
    return isinstance(a, list) and len(a) == len(b) and all(abs(x - y) <= tol for x, y in zip(a, b))


def close_matrix(a, b, tol=1e-9):
    return isinstance(a, list) and len(a) == len(b) and all(close(r, s, tol) for r, s in zip(a, b))


def raises_value_error(fn, *args):
    try:
        fn(*args)
    except ValueError:
        return True
    except Exception:
        return False
    return False


def matmul(A, B):
    return [[sum(A[i][k] * B[k][j] for k in range(len(B))) for j in range(len(B[0]))] for i in range(len(A))]


def matvec(A, x):
    return [sum(a * v for a, v in zip(row, x)) for row in A]


def identity(n):
    return [[1.0 if i == j else 0.0 for j in range(n)] for i in range(n)]


def dominant(n, shift=0):
    """Детерминированная невырожденная матрица с диагональным преобладанием."""
    return [[float(((i * 5 + j * 3 + shift) % 7) - 3) + (10.0 if i == j else 0.0) for j in range(n)] for i in range(n)]


def t_solve_basic():
    got = m.solve([[2.0, 1.0], [1.0, -1.0]], [5.0, 1.0])
    if not close(got, [2.0, 1.0]):
        return f"система 2x + y = 5, x - y = 1 имеет решение (2, 1), получили {got!r}"
    A = [[2.0, 1.0, 1.0], [1.0, 2.0, 0.0], [3.0, 0.0, 1.0]]
    got = m.solve(A, [260.0, 200.0, 220.0])
    if not close(got, [40.0, 80.0, 100.0]):
        return f"цены из трёх чеков: [40, 80, 100], получили {got!r}"
    A = dominant(4)
    x = [1.0, -2.0, 0.5, 3.0]
    got = m.solve(A, matvec(A, x))
    if not close(got, x, 1e-9):
        return f"решение системы 4 x 4, построенной по известному x = {x}, получили {got!r}"


def t_solve_swap():
    got = m.solve([[0.0, 1.0], [1.0, 0.0]], [3.0, 4.0])
    if not close(got, [4.0, 3.0]):
        return f"при A[0][0] = 0 нужна перестановка строк: x = [4, 3], получили {got!r}"
    A = [[0.0, 2.0, 1.0], [1.0, 1.0, 1.0], [2.0, 1.0, 1.0]]
    got = m.solve(A, [7.0, 6.0, 7.0])
    if not close(got, [1.0, 2.0, 3.0]):
        return f"система 3 x 3 с нулём на месте первого ведущего: x = [1, 2, 3], получили {got!r}"
    A = [[1e-4, 1.0], [1.0, 1.0]]
    x0 = 1.0 / 0.9999
    got = m.solve(A, [1.0, 2.0])
    if not close(got, [x0, 2.0 - x0], 1e-9):
        return f"система 1e-4 x + y = 1, x + y = 2: x = {x0:.10f}, y = {2.0 - x0:.10f}; получили {got!r}"


def t_det_inverse():
    got = m.determinant([[3.0, 8.0], [4.0, 6.0]])
    if abs(got - (-14.0)) > 1e-9:
        return f"det [[3, 8], [4, 6]] = 3*6 - 8*4 = -14, получили {got!r}"
    got = m.determinant([[2.0, 1.0, 1.0], [1.0, 2.0, 0.0], [3.0, 0.0, 1.0]])
    if abs(got - (-3.0)) > 1e-9:
        return f"определитель матрицы чеков равен -3, получили {got!r}"
    got = m.determinant([[0.0, 1.0], [1.0, 0.0]])
    if abs(got - (-1.0)) > 1e-9:
        return f"перестановка двух строк меняет знак: det [[0, 1], [1, 0]] = -1, получили {got!r}"
    got = m.inverse([[4.0, 7.0], [2.0, 6.0]])
    if not close_matrix(got, [[0.6, -0.7], [-0.2, 0.4]]):
        return f"обратная к [[4, 7], [2, 6]] - [[0.6, -0.7], [-0.2, 0.4]], получили {got!r}"
    A = dominant(3, 2)
    prod = matmul(A, m.inverse(A))
    if not close_matrix(prod, identity(3), 1e-9):
        return "A, умноженная на inverse(A), должна давать единичную матрицу"


def t_errors():
    sq = [[1.0, 2.0], [3.0, 4.0]]
    if not raises_value_error(m.solve, [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]], [1.0, 2.0]):
        return "solve для неквадратной матрицы - ValueError"
    if not raises_value_error(m.solve, sq, [1.0, 2.0, 3.0]):
        return "solve: длина b не равна размеру матрицы - ValueError"
    if not raises_value_error(m.solve, [[1.0, 2.0], [2.0, 4.0]], [1.0, 2.0]):
        return "solve для вырожденной матрицы [[1, 2], [2, 4]] - ValueError"
    if not raises_value_error(m.solve, [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0], [7.0, 8.0, 9.0]], [1.0, 2.0, 3.0]):
        return "solve для вырожденной матрицы 3 x 3 (строки образуют арифметическую прогрессию) - ValueError"
    if not raises_value_error(m.solve, [[0.0, 0.0], [0.0, 0.0]], [0.0, 0.0]):
        return "solve для нулевой матрицы - ValueError"
    if not raises_value_error(m.determinant, [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]):
        return "determinant для неквадратной матрицы - ValueError"
    if not raises_value_error(m.inverse, [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]):
        return "inverse для неквадратной матрицы - ValueError"
    if not raises_value_error(m.inverse, [[1.0, 2.0], [2.0, 4.0]]):
        return "inverse для вырожденной матрицы - ValueError"
    if not raises_value_error(m.inverse, [[0.0, 0.0], [0.0, 0.0]]):
        return "inverse для нулевой матрицы - ValueError"


def t_determinant_edge():
    got = m.determinant([[1.0, 2.0], [2.0, 4.0]])
    if not isinstance(got, float) or abs(got) > 1e-9:
        return f"определитель вырожденной матрицы - число 0.0, а не ошибка; получили {got!r}"
    got = m.determinant([[1.0, 2.0, 3.0], [4.0, 5.0, 6.0], [7.0, 8.0, 9.0]])
    if abs(got) > 1e-9:
        return f"det [[1, 2, 3], [4, 5, 6], [7, 8, 9]] = 0, получили {got!r}"
    got = m.determinant([[2.0, 5.0, 7.0], [0.0, 3.0, 1.0], [0.0, 0.0, 4.0]])
    if abs(got - 24.0) > 1e-9:
        return f"определитель треугольной матрицы - произведение диагонали (24), получили {got!r}"
    if abs(m.determinant([[5.0]]) - 5.0) > 1e-12:
        return "определитель матрицы 1 x 1 равен её элементу"
    A = dominant(3)
    B = dominant(3, 4)
    lhs = m.determinant(matmul(A, B))
    rhs = m.determinant(A) * m.determinant(B)
    if abs(lhs - rhs) > 1e-6 * max(1.0, abs(rhs)):
        return "det(A B) = det(A) * det(B)"
    got = m.determinant([[0.0, 2.0, 1.0], [1.0, 1.0, 1.0], [2.0, 1.0, 1.0]])
    if abs(got - 1.0) > 1e-9:
        return f"det [[0, 2, 1], [1, 1, 1], [2, 1, 1]] = 1 (нужны перестановки строк), получили {got!r}"
    got = m.determinant([[1, 2], [3, 4]])
    if abs(got - (-2.0)) > 1e-9:
        return f"определитель матрицы из целых чисел [[1, 2], [3, 4]] равен -2, получили {got!r}"


def t_inverse_edge():
    if not close_matrix(m.inverse([[4.0]]), [[0.25]]):
        return "обратная к [[4]] - [[0.25]]"
    A = [[0.0, 2.0, 1.0], [1.0, 1.0, 1.0], [2.0, 1.0, 1.0]]
    inv = m.inverse(A)
    if not close_matrix(matmul(A, inv), identity(3)) or not close_matrix(matmul(inv, A), identity(3)):
        return "A * inverse(A) и inverse(A) * A должны давать единичную матрицу (в A нужны перестановки строк)"
    A = dominant(4, 1)
    if not close_matrix(m.inverse(m.inverse(A)), A, 1e-8):
        return "обратная к обратной - исходная матрица"
    got = m.inverse([[1, 0], [0, 2]])
    if not close_matrix(got, [[1.0, 0.0], [0.0, 0.5]]):
        return f"обратная к diag(1, 2) - diag(1, 0.5), получили {got!r}"
    got = m.solve([[1.0, 0.0], [0.0, 1e-6]], [1.0, 1e-6])
    if not close(got, [1.0, 1.0], 1e-9):
        return f"матрица с малым, но не нулевым ведущим 1e-6 не вырождена: x = [1, 1], получили {got!r}"
    got = m.solve([[7.0]], [14.0])
    if not close(got, [2.0]):
        return f"система 1 x 1: 7x = 14, получили {got!r}"
    got = m.solve([[2, 1], [1, 3]], [3, 5])
    if not close(got, [0.8, 1.4]):
        return f"решение для целых чисел 2x + y = 3, x + 3y = 5: [0.8, 1.4], получили {got!r}"


def t_no_mutation():
    A = [[0.0, 2.0, 1.0], [1.0, 1.0, 1.0], [2.0, 1.0, 1.0]]
    b = [7.0, 6.0, 7.0]
    A0 = [row[:] for row in A]
    b0 = b[:]
    m.solve(A, b)
    m.determinant(A)
    r = m.inverse(A)
    if A != A0 or b != b0:
        return "функции не должны изменять A и b: работайте с копиями"
    r[0][0] = 99.0
    if A != A0:
        return "inverse должна возвращать новые списки"


case("solve-basic", t_solve_basic)
case("solve-swap", t_solve_swap)
case("det-inverse", t_det_inverse)
case("errors", t_errors)
case("determinant-edge", t_determinant_edge)
case("inverse-edge", t_inverse_edge)
case("no-mutation", t_no_mutation)
