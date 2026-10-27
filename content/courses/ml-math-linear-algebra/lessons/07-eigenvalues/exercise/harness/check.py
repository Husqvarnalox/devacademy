import json
import math
import os
from fractions import Fraction

NONCE = os.environ["DEVACADEMY_NONCE"]

import power_task as m


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
    try:
        return len(a) == len(b) and all(abs(x - y) <= tol for x, y in zip(a, b))
    except TypeError:
        return False


def raises_value_error(fn, *args, **kwargs):
    try:
        fn(*args, **kwargs)
    except ValueError:
        return True
    except Exception:
        return False
    return False


def reference_stationary(P):
    """Эталон: решаем p (P - I) = 0, sum(p) = 1 в точной арифметике (Fraction)."""
    n = len(P)
    # уравнения: для каждого j: sum_i p_i (P[i][j] - delta_ij) = 0; последнее заменяем на sum p = 1
    rows = [[Fraction(P[i][j]) - (1 if i == j else 0) for i in range(n)] + [Fraction(0)] for j in range(n)]
    rows[-1] = [Fraction(1)] * n + [Fraction(1)]
    for c in range(n):
        piv = next(r for r in range(c, n) if rows[r][c] != 0)
        rows[c], rows[piv] = rows[piv], rows[c]
        pv = rows[c][c]
        rows[c] = [v / pv for v in rows[c]]
        for r in range(n):
            if r != c and rows[r][c] != 0:
                f = rows[r][c]
                rows[r] = [a - f * b for a, b in zip(rows[r], rows[c])]
    return [float(rows[i][n]) for i in range(n)]


def up_to_sign(a, b, tol):
    return close(a, b, tol) or close(a, [-x for x in b], tol)


def t_rayleigh():
    A = [[2.0, 1.0], [1.0, 2.0]]
    cases = [([1.0, 1.0], 3.0), ([1.0, -1.0], 1.0), ([1.0, 0.0], 2.0), ([3.0, 3.0], 3.0)]
    for v, want in cases:
        got = m.rayleigh_quotient(A, v)
        if abs(got - want) > 1e-12:
            return f"v^T A v / v^T v для A = [[2, 1], [1, 2]] и v = {v} равно {want}, получили {got!r}"
    B = [[1.0, 2.0], [0.0, 3.0]]
    got = m.rayleigh_quotient(B, [1.0, 1.0])
    if abs(got - 3.0) > 1e-12:
        return f"для несимметричной B = [[1, 2], [0, 3]] и v = [1, 1]: (v . Bv) / (v . v) = 6 / 2 = 3, получили {got!r}"


def t_power_symmetric():
    lam, v = m.power_iteration([[2.0, 1.0], [1.0, 2.0]])
    s = 1 / math.sqrt(2)
    if abs(lam - 3.0) > 1e-9 or not close(v, [s, s], 1e-8):
        return f"для [[2, 1], [1, 2]] ожидалось lam = 3, v = (1, 1)/sqrt(2); получили {lam!r}, {v!r}"
    lam, v = m.power_iteration([[1.0, 0.0, 0.0], [0.0, 7.0, 0.0], [0.0, 0.0, 3.0]])
    if abs(lam - 7.0) > 1e-9 or not close(v, [0.0, 1.0, 0.0], 1e-8):
        return f"для diag(1, 7, 3) ожидалось lam = 7, v = (0, 1, 0); получили {lam!r}, {v!r}"
    # [[5, 1], [1, 3]]: lam = 4 + sqrt(2), v ~ (1, sqrt(2) - 1)
    lam, v = m.power_iteration([[5.0, 1.0], [1.0, 3.0]])
    r = math.sqrt(2) - 1
    norm = math.hypot(1.0, r)
    if abs(lam - (4 + math.sqrt(2))) > 1e-8 or not close(v, [1 / norm, r / norm], 1e-7):
        return f"для [[5, 1], [1, 3]] ожидалось lam = {4 + math.sqrt(2)}, v = {[1 / norm, r / norm]}; получили {lam!r}, {v!r}"
    if abs(math.sqrt(sum(x * x for x in v)) - 1.0) > 1e-12:
        return "вектор v должен иметь длину 1"


def t_power_negative():
    lam, v = m.power_iteration([[-3.0, 0.0], [0.0, 1.0]])
    if abs(lam - (-3.0)) > 1e-8:
        return f"доминирующее по модулю значение diag(-3, 1) равно -3, получили {lam!r}"
    if not close(v, [1.0, 0.0], 1e-7):
        return f"после фиксации знака (наибольшая по модулю компонента положительна) ожидалось v = (1, 0), получили {v!r}"


def t_is_eigenpair():
    A = [[2.0, 1.0], [1.0, 2.0]]
    if not m.is_eigenpair(A, 3.0, [1.0, 1.0]):
        return "(3, (1, 1)) - собственная пара [[2, 1], [1, 2]]"
    if not m.is_eigenpair(A, 1.0, [2.0, -2.0]):
        return "(1, (2, -2)) - собственная пара (длина вектора не обязана быть равной 1)"
    if m.is_eigenpair(A, 3.0, [1.0, 0.0]):
        return "(3, (1, 0)) - не собственная пара: A (1, 0) = (2, 1)"
    if m.is_eigenpair(A, 2.0, [1.0, 1.0]):
        return "(2, (1, 1)) - не собственная пара: A (1, 1) = (3, 3)"
    if m.is_eigenpair(A, 0.0, [0.0, 0.0]) or m.is_eigenpair(A, 3.0, [0.0, 0.0]):
        return "нулевой вектор собственным не считается"
    big = [[1000.0, 0.0], [0.0, 1.0]]
    if not m.is_eigenpair(big, 1000.0 + 1e-6, [1.0, 0.0]):
        return "допуск масштабируется на max(1, |lam|): при lam = 1000 отклонение 1e-6 допустимо"
    if m.is_eigenpair(big, 1000.5, [1.0, 0.0]):
        return "отклонение значения на 0.5 слишком велико"


def t_stationary():
    P = [[0.9, 0.1], [0.5, 0.5]]
    got = m.stationary_distribution(P)
    if not close(got, [5 / 6, 1 / 6], 1e-9):
        return f"для [[0.9, 0.1], [0.5, 0.5]] стационарное распределение [5/6, 1/6], получили {got!r}"
    P3 = [[0.5, 0.25, 0.25], [0.2, 0.6, 0.2], [0.1, 0.3, 0.6]]
    got = m.stationary_distribution(P3)
    want = reference_stationary(P3)
    if not close(got, want, 1e-9):
        return f"ожидалось {want!r}, получили {got!r}"
    if abs(sum(got) - 1.0) > 1e-12:
        return "сумма вероятностей должна оставаться равной 1"
    pP = [sum(got[i] * P3[i][j] for i in range(3)) for j in range(3)]
    if not close(pP, got, 1e-9):
        return "должно выполняться p = p P"
    # сложная цепь: медленное перемешивание, нужно много шагов
    slow = [[0.98, 0.02], [0.03, 0.97]]
    got = m.stationary_distribution(slow)
    if not close(got, [0.6, 0.4], 1e-8):
        return f"для [[0.98, 0.02], [0.03, 0.97]] стационарное распределение [0.6, 0.4], получили {got!r}"


def t_errors():
    if not raises_value_error(m.power_iteration, [[0.0, 0.0], [0.0, 0.0]]):
        return "нулевая матрица: A v = 0, ожидался ValueError"
    if not raises_value_error(m.power_iteration, [[0.0, 1.0], [0.0, 0.0]]):
        return "для [[0, 1], [0, 0]] после первого шага A v = 0, ожидался ValueError"
    if not raises_value_error(m.rayleigh_quotient, [[1.0, 0.0], [0.0, 1.0]], [0.0, 0.0]):
        return "rayleigh_quotient для нулевого вектора - ValueError"


def t_no_mutation_and_ints():
    A = [[2, 1], [1, 2]]
    A0 = [row[:] for row in A]
    lam, v = m.power_iteration(A)
    m.rayleigh_quotient(A, [1, 2])
    m.is_eigenpair(A, 3, [1, 1])
    P = [[0.9, 0.1], [0.5, 0.5]]
    P0 = [row[:] for row in P]
    m.stationary_distribution(P)
    if A != A0 or P != P0:
        return "функции не должны изменять матрицы"
    if abs(lam - 3.0) > 1e-9:
        return f"целочисленная матрица [[2, 1], [1, 2]]: ожидалось lam = 3, получили {lam!r}"
    # num_iters ограничивает число шагов: после одного шага из (1, 0)-подобного старта результат ещё неточен
    B = [[2.0, 1.0], [1.0, 3.0]]
    lam1, v1 = m.power_iteration(B, num_iters=1)
    lam_full, _ = m.power_iteration(B)
    if abs(lam_full - (5 + math.sqrt(5)) / 2) > 1e-8:
        return f"для [[2, 1], [1, 3]] ожидалось lam = (5 + sqrt(5)) / 2, получили {lam_full!r}"
    if abs(lam1 - lam_full) < 1e-3:
        return "при num_iters = 1 выполняется ровно один шаг, значение ещё не сошлось"
    if abs(math.sqrt(sum(x * x for x in v1)) - 1.0) > 1e-12:
        return "и после одного шага вектор нормирован"


case("rayleigh", t_rayleigh)
case("power-symmetric", t_power_symmetric)
case("power-negative", t_power_negative)
case("is-eigenpair", t_is_eigenpair)
case("stationary", t_stationary)
case("errors", t_errors)
case("no-mutation-ints", t_no_mutation_and_ints)
