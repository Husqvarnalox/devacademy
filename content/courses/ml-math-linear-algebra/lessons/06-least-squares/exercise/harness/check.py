import json
import math
import os
import random
from fractions import Fraction

NONCE = os.environ["DEVACADEMY_NONCE"]

import normal_eq_task as m


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


def reference_fit(X, y, l2=0.0):
    """Независимый эталон: нормальные уравнения в точной арифметике (Fraction), метод Гаусса-Жордана."""
    Xb = [[Fraction(1)] + [Fraction(v) for v in row] for row in X]
    d1 = len(Xb[0])
    A = [[sum(r[i] * r[j] for r in Xb) + (Fraction(l2) if i == j and i > 0 else 0) for j in range(d1)] for i in range(d1)]
    b = [sum(r[i] * Fraction(yv) for r, yv in zip(Xb, y)) for i in range(d1)]
    M = [A[i] + [b[i]] for i in range(d1)]
    for c in range(d1):
        p = next(r for r in range(c, d1) if M[r][c] != 0)
        M[c], M[p] = M[p], M[c]
        piv = M[c][c]
        M[c] = [v / piv for v in M[c]]
        for r in range(d1):
            if r != c and M[r][c] != 0:
                f = M[r][c]
                M[r] = [a - f * bb for a, bb in zip(M[r], M[c])]
    return [float(M[i][d1]) for i in range(d1)]


def noisy_data():
    rnd = random.Random(11)
    X = [[rnd.uniform(-2, 2), rnd.uniform(0, 5)] for _ in range(12)]
    y = [3.0 + 1.5 * a - 2.0 * b + rnd.gauss(0, 0.3) for a, b in X]
    return X, y


def t_exact_line():
    X = [[0.0], [1.0], [2.0], [3.0]]
    y = [1.0, 3.0, 5.0, 7.0]
    got = m.fit_normal_equation(X, y)
    if not close(got, [1.0, 2.0]):
        return f"точки лежат на прямой y = 2x + 1, ожидалось w = [1, 2], получили {got!r}"
    xb = m.add_bias_column(X)
    if xb != [[1.0, 0.0], [1.0, 1.0], [1.0, 2.0], [1.0, 3.0]]:
        return f"add_bias_column должна дописать 1.0 в начало каждой строки, получили {xb!r}"


def t_two_features_vs_reference():
    X, y = noisy_data()
    got = m.fit_normal_equation(X, y)
    want = reference_fit(X, y)
    if not close(got, want, 1e-8):
        return f"веса должны совпасть с решением нормальных уравнений {want!r}, получили {got!r}"
    pred = m.predict([[1.0, 2.0]], got)
    if len(pred) != 1 or abs(pred[0] - (want[0] + want[1] + 2 * want[2])) > 1e-8:
        return f"predict для x = [1, 2] должна вернуть w[0] + w[1] * 1 + w[2] * 2, получили {pred!r}"


def t_residual_orthogonal():
    X, y = noisy_data()
    w = m.fit_normal_equation(X, y)
    Xb = m.add_bias_column(X)
    pred = m.predict(X, w)
    r = [a - b for a, b in zip(y, pred)]
    for j in range(len(Xb[0])):
        s = sum(row[j] * ri for row, ri in zip(Xb, r))
        if abs(s) > 1e-8:
            return f"остаток y - Xw должен быть ортогонален каждому столбцу Xb, для столбца {j} скалярное произведение равно {s:.3g}"
    want = sum(ri * ri for ri in r) / len(r)
    if abs(m.mse(y, pred) - want) > 1e-12:
        return f"mse должна быть средним квадратом остатков {want}, получили {m.mse(y, pred)}"


def t_collinear():
    X = [[1.0, 1.0], [2.0, 2.0], [3.0, 3.0], [4.0, 4.0], [5.0, 5.0]]
    y = [2.1, 3.9, 6.2, 7.8, 10.1]
    if not raises_value_error(m.fit_normal_equation, X, y):
        return "признаки-дубликаты дают вырожденную XbT Xb: без l2 ожидался ValueError из solve"
    got = m.fit_normal_equation(X, y, l2=0.5)
    want = reference_fit(X, y, l2=0.5)
    if not all(math.isfinite(v) for v in got) or not close(got, want, 1e-8):
        return f"с l2 = 0.5 система разрешима, ожидалось {want!r}, получили {got!r}"
    if abs(got[1] - got[2]) > 1e-8:
        return "у одинаковых признаков веса при ridge должны совпадать"


def t_intercept_not_penalized():
    X = [[0.0, 1.0], [1.0, -1.0], [2.0, 3.0], [3.0, 0.5], [4.0, 2.0]]
    y = [7.0, 7.0, 7.0, 7.0, 7.0]
    got = m.fit_normal_equation(X, y, l2=1000.0)
    if not close(got, [7.0, 0.0, 0.0], 1e-8):
        return f"для постоянного y = 7 свободный член штрафовать нельзя: ожидалось [7, 0, 0], получили {got!r}"
    X2, y2 = noisy_data()
    got2 = m.fit_normal_equation(X2, y2, l2=2.0)
    want2 = reference_fit(X2, y2, l2=2.0)
    if not close(got2, want2, 1e-8):
        return f"ridge с l2 = 2 на шумных данных: ожидалось {want2!r}, получили {got2!r}"


def t_ridge_shrinks():
    X, y = noisy_data()
    norms = []
    for l2 in (0.0, 1.0, 10.0, 100.0):
        w = m.fit_normal_equation(X, y, l2=l2)
        norms.append(math.sqrt(sum(v * v for v in w[1:])))
    if not all(a > b for a, b in zip(norms, norms[1:])):
        return f"с ростом l2 норма весов без свободного члена должна убывать, получили {norms!r}"


def t_no_mutation_and_edges():
    X = [[1, 2], [2, 1], [3, 5], [4, 3]]
    y = [5, 4, 9, 8]
    X0 = [row[:] for row in X]
    y0 = y[:]
    w = m.fit_normal_equation(X, y, l2=0.1)
    xb = m.add_bias_column(X)
    if X != X0 or y != y0:
        return "функции не должны изменять X и y"
    if xb is X or any(r is o for r, o in zip(xb, X)):
        return "add_bias_column должна вернуть новые строки"
    if len(w) != 3:
        return f"для двух признаков w должен иметь длину 3, получили {len(w)}"
    if m.mse([1, 2, 3], [1, 2, 3]) != 0:
        return "mse идеального предсказания равна 0"
    if abs(m.mse([0, 0], [1, 3]) - 5.0) > 1e-12:
        return "mse([0, 0], [1, 3]) = (1 + 9) / 2 = 5"
    if not close(m.predict([[1, 2], [0, 0]], [1.0, 2.0, 3.0]), [9.0, 1.0]):
        return "predict с w = [1, 2, 3]: для [1, 2] получится 1 + 2 + 6 = 9, для [0, 0] - 1"
    if not raises_value_error(m.fit_normal_equation, [[1.0], [1.0], [1.0]], [1.0, 2.0, 3.0]):
        return "постоянный признак совпадает со столбцом единиц: без l2 ожидался ValueError"


case("exact-line", t_exact_line)
case("two-features", t_two_features_vs_reference)
case("residual-orthogonal", t_residual_orthogonal)
case("collinear-features", t_collinear)
case("intercept-not-penalized", t_intercept_not_penalized)
case("ridge-shrinks", t_ridge_shrinks)
case("no-mutation-edges", t_no_mutation_and_edges)
