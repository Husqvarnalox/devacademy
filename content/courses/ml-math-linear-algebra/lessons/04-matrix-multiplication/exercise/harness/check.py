import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import matmul_task as m


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


def close_matrix(a, b, tol=1e-9):
    return (
        isinstance(a, list)
        and len(a) == len(b)
        and all(isinstance(r, list) and len(r) == len(s) and all(abs(x - y) <= tol for x, y in zip(r, s))
                for r, s in zip(a, b))
    )


def raises_value_error(fn, *args):
    try:
        fn(*args)
    except ValueError:
        return True
    except Exception:
        return False
    return False


def ref_matmul(A, B):
    return [[sum(A[i][k] * B[k][j] for k in range(len(B))) for j in range(len(B[0]))] for i in range(len(A))]


def matvec(A, x):
    return [sum(a * v for a, v in zip(row, x)) for row in A]


def t_matmul_basic():
    A = [[1.0, 2.0, 0.0], [0.0, 1.0, 3.0]]
    B = [[1.0, 0.0], [2.0, 1.0], [0.0, 4.0]]
    got = m.matmul(A, B)
    if not close_matrix(got, [[5.0, 2.0], [2.0, 13.0]]):
        return f"A (2 x 3) на B (3 x 2) даёт [[5, 2], [2, 13]], получили {got!r}"
    got = m.matmul(B, A)
    want = [[1.0, 2.0, 0.0], [2.0, 5.0, 3.0], [0.0, 4.0, 12.0]]
    if not close_matrix(got, want):
        return f"B (3 x 2) на A (2 x 3) даёт матрицу 3 x 3 {want}, получили {got!r}"
    I = [[1.0, 0.0], [0.0, 1.0]]
    got = m.matmul([[2.0, -1.0], [4.0, 3.0]], I)
    if not close_matrix(got, [[2.0, -1.0], [4.0, 3.0]]):
        return "умножение на единичную матрицу не меняет матрицу"
    S, T = [[1.0, 1.0], [0.0, 1.0]], [[1.0, 0.0], [1.0, 1.0]]
    if close_matrix(m.matmul(S, T), m.matmul(T, S)):
        return "умножение матриц не перестановочно: ST и TS для сдвигов S, T различаются"


def t_identity_power():
    got = m.identity(3)
    if not close_matrix(got, [[1.0, 0.0, 0.0], [0.0, 1.0, 0.0], [0.0, 0.0, 1.0]]):
        return f"identity(3) - единицы на диагонали, получили {got!r}"
    F = [[1.0, 1.0], [1.0, 0.0]]
    got = m.matrix_power(F, 10)
    if not close_matrix(got, [[89.0, 55.0], [55.0, 34.0]]):
        return f"[[1, 1], [1, 0]] в десятой степени - числа Фибоначчи [[89, 55], [55, 34]], получили {got!r}"
    A = [[2.0, 1.0], [0.0, 3.0]]
    if not close_matrix(m.matrix_power(A, 0), [[1.0, 0.0], [0.0, 1.0]]):
        return "нулевая степень - единичная матрица"
    if not close_matrix(m.matrix_power(A, 1), A):
        return "первая степень равна самой матрице"
    if not close_matrix(m.matrix_power(A, 3), [[8.0, 19.0], [0.0, 27.0]]):
        return f"[[2, 1], [0, 3]] в кубе - [[8, 19], [0, 27]], получили {m.matrix_power(A, 3)!r}"


def t_compose():
    W1 = [[1.0, -1.0, 2.0], [0.5, 0.0, 1.0]]
    W2 = [[2.0, 1.0], [0.0, -1.0], [1.0, 3.0]]
    got = m.compose_linear_layers([W1, W2])
    want = ref_matmul(W2, W1)
    if not close_matrix(got, want):
        return f"слои W1, затем W2 дают матрицу W2 W1 = {want}, получили {got!r}"
    x = [1.0, 2.0, 3.0]
    seq = matvec(W2, matvec(W1, x))
    one = matvec(got, x)
    if any(abs(a - b) > 1e-9 for a, b in zip(seq, one)):
        return "объединённая матрица должна давать тот же результат, что и два слоя подряд"


def t_matmul_errors():
    A = [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]
    if not raises_value_error(m.matmul, A, A):
        return "matmul: (2 x 3) на (2 x 3) не определено - ValueError"
    if not raises_value_error(m.matmul, [[1.0, 2.0]], [[1.0, 2.0, 3.0]]):
        return "matmul: (1 x 2) на (1 x 3) - ValueError"
    if not raises_value_error(m.matmul, [], [[1.0]]):
        return "matmul с пустой матрицей - ValueError"
    if not raises_value_error(m.matmul, [[1.0, 2.0], [3.0]], [[1.0], [2.0]]):
        return "matmul с рваной матрицей - ValueError"
    if not raises_value_error(m.compose_linear_layers, []):
        return "compose_linear_layers([]) - ValueError"
    W1 = [[1.0, 2.0], [3.0, 4.0]]
    W2 = [[1.0, 2.0, 3.0]]
    if not raises_value_error(m.compose_linear_layers, [W1, W1, W2, W2]):
        return "compose_linear_layers: после слоя с 2 выходами нельзя применять слой с 3 входами - ValueError"


def t_power_errors():
    if not raises_value_error(m.matrix_power, [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]], 2):
        return "matrix_power для матрицы 2 x 3 - ValueError (нужна квадратная)"
    if not raises_value_error(m.matrix_power, [[1.0, 0.0], [0.0, 1.0]], -1):
        return "matrix_power с k < 0 - ValueError"
    if not raises_value_error(m.identity, 0):
        return "identity(0) - ValueError"
    if not raises_value_error(m.identity, -3):
        return "identity(-3) - ValueError"


def t_no_mutation():
    A = [[1.0, 2.0], [3.0, 4.0]]
    B = [[0.0, 1.0], [1.0, 0.0]]
    A0 = [row[:] for row in A]
    B0 = [row[:] for row in B]
    C = m.matmul(A, B)
    P0 = m.matrix_power(A, 0)
    P1 = m.matrix_power(A, 1)
    P5 = m.matrix_power(A, 5)
    L = m.compose_linear_layers([A])
    L2 = m.compose_linear_layers([A, B])
    if A != A0 or B != B0:
        return "функции не должны изменять входные матрицы"
    P1[0][0] = 99.0
    L[0][0] = 99.0
    P0[0][0] = 99.0
    C[0][0] = 99.0
    L2[0][0] = 99.0
    P5[0][0] = 99.0
    if A != A0 or B != B0:
        return "результаты должны быть новыми списками (matrix_power(A, 1) и compose_linear_layers([A]) не должны возвращать A)"
    if m.identity(2) is m.identity(2):
        return "identity должна каждый раз создавать новую матрицу"


def t_edge_cases():
    if not close_matrix(m.matmul([[3.0]], [[4.0]]), [[12.0]]):
        return "матрицы 1 x 1: 3 * 4 = 12"
    got = m.matmul([[1, 2, 3]], [[4], [5], [6]])
    if not close_matrix(got, [[32.0]]):
        return f"строка (1 x 3) на столбец (3 x 1) - матрица 1 x 1 со скалярным произведением 32, получили {got!r}"
    got = m.matmul([[4], [5], [6]], [[1, 2, 3]])
    if not close_matrix(got, [[4.0, 8.0, 12.0], [5.0, 10.0, 15.0], [6.0, 12.0, 18.0]]):
        return f"столбец (3 x 1) на строку (1 x 3) - матрица 3 x 3, получили {got!r}"
    got = m.matmul([[1, 2], [3, 4]], [[0, 1], [1, 0]])
    if not close_matrix(got, [[2.0, 1.0], [4.0, 3.0]]):
        return f"умножение справа на перестановку меняет столбцы местами: [[2, 1], [4, 3]], получили {got!r}"
    if not close_matrix(m.matrix_power([[2.0]], 10), [[1024.0]]):
        return "2 в десятой степени как матрица 1 x 1 - 1024"
    P = [[0.9, 0.1], [0.5, 0.5]]
    P50 = m.matrix_power(P, 50)
    if not close_matrix(P50, [[5.0 / 6.0, 1.0 / 6.0], [5.0 / 6.0, 1.0 / 6.0]], tol=1e-6):
        return f"P в 50-й степени для матрицы переходов погоды близка к [[5/6, 1/6], [5/6, 1/6]], получили {P50!r}"
    if any(abs(sum(row) - 1.0) > 1e-9 for row in m.matrix_power(P, 7)):
        return "строки степени стохастической матрицы по-прежнему суммируются в 1"
    Z = [[0.0, 1.0], [0.0, 0.0]]
    if not close_matrix(m.matrix_power(Z, 2), [[0.0, 0.0], [0.0, 0.0]]):
        return "[[0, 1], [0, 0]] в квадрате - нулевая матрица"
    Ws = [[[1.0, 2.0, 0.0], [0.0, 1.0, 1.0]], [[1.0, 0.0], [2.0, 1.0], [0.0, 3.0]], [[1.0, 1.0, 1.0]]]
    got = m.compose_linear_layers(Ws)
    want = ref_matmul(Ws[2], ref_matmul(Ws[1], Ws[0]))
    if not close_matrix(got, want):
        return f"три слоя: Ws[2] Ws[1] Ws[0] = {want}, получили {got!r}"
    left = ref_matmul(ref_matmul(Ws[2], Ws[1]), Ws[0])
    if not close_matrix(got, left):
        return "ассоциативность: порядок скобок не меняет результат"


case("matmul-basic", t_matmul_basic)
case("identity-power", t_identity_power)
case("compose", t_compose)
case("matmul-errors", t_matmul_errors)
case("power-errors", t_power_errors)
case("no-mutation", t_no_mutation)
case("edge-cases", t_edge_cases)
