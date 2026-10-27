import json
import math
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import svd_task as m


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


def matvec(A, v):
    return [sum(a * x for a, x in zip(row, v)) for row in A]


def norm(v):
    return math.sqrt(sum(x * x for x in v))


def sigma_two_columns(A):
    """Эталон: наибольшее сингулярное число матрицы с двумя столбцами через собственные значения A^T A (2 x 2)."""
    a = sum(r[0] * r[0] for r in A)
    b = sum(r[0] * r[1] for r in A)
    c = sum(r[1] * r[1] for r in A)
    lam = (a + c) / 2 + math.sqrt(((a - c) / 2) ** 2 + b * b)
    return math.sqrt(lam)


def ref_direction_two_columns(A):
    """Эталон: главный собственный вектор A^T A (2 x 2) единичной длины, наибольшая по модулю компонента > 0."""
    a = sum(r[0] * r[0] for r in A)
    b = sum(r[0] * r[1] for r in A)
    c = sum(r[1] * r[1] for r in A)
    lam = (a + c) / 2 + math.sqrt(((a - c) / 2) ** 2 + b * b)
    v = [b, lam - a] if abs(b) > 1e-15 else ([1.0, 0.0] if a >= c else [0.0, 1.0])
    n = norm(v)
    v = [x / n for x in v]
    k = 0 if abs(v[0]) >= abs(v[1]) else 1
    return [-x for x in v] if v[k] < 0 else v


LINE_X = [[float(i), 2.0 * i + e] for i, e in zip(
    range(-5, 6), [0.1, -0.2, 0.15, -0.05, 0.2, 0.0, -0.1, 0.05, -0.15, 0.1, -0.2])]


def t_singular_diag():
    sigma, u, v = m.top_singular_triplet([[3.0, 0.0], [0.0, 1.0]])
    if abs(sigma - 3.0) > 1e-9 or not close(v, [1.0, 0.0], 1e-8) or not close(u, [1.0, 0.0], 1e-8):
        return f"для diag(3, 1) ожидалось sigma = 3, u = v = (1, 0); получили {sigma!r}, {u!r}, {v!r}"
    sigma, u, v = m.top_singular_triplet([[3.0, 0.0], [0.0, 1.0], [0.0, 0.0]])
    if abs(sigma - 3.0) > 1e-9 or len(u) != 3 or not close(u, [1.0, 0.0, 0.0], 1e-8):
        return f"для [[3, 0], [0, 1], [0, 0]] вектор u имеет длину 3 (число строк): ожидалось (1, 0, 0), получили {u!r}"
    sigma, u, v = m.top_singular_triplet([[1.0, 1.0], [1.0, 1.0]])
    s = 1 / math.sqrt(2)
    if abs(sigma - 2.0) > 1e-9 or not close(v, [s, s], 1e-8) or not close(u, [s, s], 1e-8):
        return f"для [[1, 1], [1, 1]] ожидалось sigma = 2, u = v = (1, 1)/sqrt(2); получили {sigma!r}, {u!r}, {v!r}"


def t_sigma_matches_reference():
    A = [[3.0, 1.0], [1.0, 2.0], [0.0, 1.0], [2.0, 2.0], [-1.0, 0.5]]
    sigma, u, v = m.top_singular_triplet(A)
    want = sigma_two_columns(A)
    if abs(sigma - want) > 1e-8:
        return f"sigma должна быть спектральной нормой ||A||_2 = {want}, получили {sigma!r}"
    if not close(v, ref_direction_two_columns(A), 1e-7):
        return "v должен быть главным собственным вектором A^T A (длина 1, наибольшая по модулю компонента положительна)"
    if abs(norm(u) - 1.0) > 1e-9 or abs(norm(v) - 1.0) > 1e-9:
        return "u и v должны иметь длину 1"
    Av = matvec(A, v)
    if not close(Av, [sigma * x for x in u], 1e-8):
        return "должно выполняться A v = sigma u"
    # матрица с тремя столбцами: проверяем определяющие свойства
    B = [[2.0, 0.0, 1.0], [1.0, 3.0, 0.0], [0.0, 1.0, 1.0], [1.0, 1.0, 2.0]]
    sigma, u, v = m.top_singular_triplet(B)
    BtBv = [sum(B[i][j] * matvec(B, v)[i] for i in range(len(B))) for j in range(3)]
    if not close(BtBv, [sigma * sigma * x for x in v], 1e-7):
        return "для главной тройки должно выполняться A^T A v = sigma^2 v"
    cols = [norm([B[i][j] for i in range(len(B))]) for j in range(3)]
    fro2 = sum(x * x for row in B for x in row)
    if not (max(cols) <= sigma + 1e-9 and sigma * sigma <= fro2 + 1e-9):
        return "sigma не меньше нормы любого столбца и sigma^2 не больше суммы квадратов всех элементов"


def t_rank_one():
    u0 = [1.0, 2.0, 3.0, -1.0]
    v0 = [2.0, -1.0, 0.5]
    A = [[a * b for b in v0] for a in u0]
    got = m.rank_one_approximation(A)
    if len(got) != 4 or any(len(r) != 3 for r in got):
        return "результат должен иметь ту же форму, что и A (4 строки по 3 числа)"
    if any(abs(x - y) > 1e-9 for r1, r2 in zip(got, A) for x, y in zip(r1, r2)):
        return "матрица ранга 1 должна восстанавливаться точно"
    got = m.rank_one_approximation([[3.0, 0.0], [0.0, 1.0]])
    if any(abs(x - y) > 1e-9 for r1, r2 in zip(got, [[3.0, 0.0], [0.0, 0.0]]) for x, y in zip(r1, r2)):
        return f"лучшее приближение ранга 1 для diag(3, 1) - diag(3, 0), получили {got!r}"


def t_principal_component():
    mean, comp = m.first_principal_component(LINE_X)
    n = len(LINE_X)
    want_mean = [sum(r[0] for r in LINE_X) / n, sum(r[1] for r in LINE_X) / n]
    if not close(mean, want_mean, 1e-12):
        return f"mean должен быть средним по столбцам {want_mean!r}, получили {mean!r}"
    centered = [[r[0] - want_mean[0], r[1] - want_mean[1]] for r in LINE_X]
    want = ref_direction_two_columns(centered)
    if not close(comp, want, 1e-7):
        return f"компонента должна быть главным вектором центрированных данных {want!r}, получили {comp!r}"
    s5 = math.sqrt(5)
    if not close(comp, [1 / s5, 2 / s5], 0.02):
        return "точки лежат около прямой y = 2x: компонента близка к (1, 2)/sqrt(5)"
    mean2, comp2 = m.first_principal_component([[0.0, 0.0], [1.0, -1.0], [2.0, -2.0], [3.0, -3.0]])
    s2 = math.sqrt(2)
    if not (close(comp2, [1 / s2, -1 / s2], 1e-9) or close(comp2, [-1 / s2, 1 / s2], 1e-9)):
        return f"точки на прямой y = -x дают компоненту ±(1, -1)/sqrt(2), получили {comp2!r}"


def t_project():
    mean, comp = m.first_principal_component([[0.0, 0.0], [1.0, 1.0], [2.0, 2.0]])
    s = 1 / math.sqrt(2)
    if not close(mean, [1.0, 1.0], 1e-12) or not close(comp, [s, s], 1e-8):
        return f"для точек на диагонали ожидались mean = (1, 1), component = (1, 1)/sqrt(2); получили {mean!r}, {comp!r}"
    got = m.project([[0.0, 0.0], [1.0, 1.0], [2.0, 2.0]], mean, comp)
    if not close(got, [-math.sqrt(2), 0.0, math.sqrt(2)], 1e-8):
        return f"координаты точек вдоль диагонали: [-sqrt(2), 0, sqrt(2)], получили {got!r}"
    mean, comp = m.first_principal_component(LINE_X)
    scores = m.project(LINE_X, mean, comp)
    if abs(sum(scores) / len(scores)) > 1e-9:
        return "среднее координат центрированных данных вдоль любой компоненты равно 0"
    a = [[r[0] - mean[0], r[1] - mean[1]] for r in LINE_X]
    if abs(sum(s * s for s in scores) - sigma_two_columns(a) ** 2) > 1e-6:
        return "сумма квадратов координат вдоль главной компоненты равна sigma_1^2 центрированных данных"


def t_errors():
    if not raises_value_error(m.top_singular_triplet, [[0.0, 0.0], [0.0, 0.0]]):
        return "нулевая матрица: ожидался ValueError"
    if not raises_value_error(m.rank_one_approximation, [[0.0, 0.0, 0.0]]):
        return "приближение ранга 1 нулевой матрицы - ValueError"
    if not raises_value_error(m.first_principal_component, [[1.0, 2.0]]):
        return "один объект: главную компоненту найти нельзя, ожидался ValueError"
    if not raises_value_error(m.first_principal_component, [[1.0, 1.0], [1.0, 1.0], [1.0, 1.0]]):
        return "одинаковые объекты: центрированные данные нулевые, ожидался ValueError"


def t_no_mutation_ints():
    A = [[3, 1], [1, 2], [0, 1]]
    A0 = [r[:] for r in A]
    X = [[1, 2], [2, 4], [3, 7], [4, 8]]
    X0 = [r[:] for r in X]
    sigma, u, v = m.top_singular_triplet(A)
    approx = m.rank_one_approximation(A)
    mean, comp = m.first_principal_component(X)
    scores = m.project(X, mean, comp)
    if A != A0 or X != X0:
        return "функции не должны изменять входные матрицы"
    if approx is A or any(r is o for r, o in zip(approx, A)):
        return "rank_one_approximation должна вернуть новую матрицу"
    if abs(sigma - sigma_two_columns(A)) > 1e-8:
        return "целочисленная матрица обрабатывается как обычная"
    if abs(sum(scores)) > 1e-9 or len(scores) != 4:
        return "project возвращает по одному числу на объект, их сумма для центрированных данных равна 0"
    if abs(norm(comp) - 1.0) > 1e-9:
        return "компонента имеет длину 1"


case("singular-diag", t_singular_diag)
case("sigma-reference", t_sigma_matches_reference)
case("rank-one", t_rank_one)
case("principal-component", t_principal_component)
case("project-scores", t_project)
case("errors", t_errors)
case("no-mutation-ints", t_no_mutation_ints)
