import math


def _matvec(A, v):
    return [sum(a * x for a, x in zip(row, v)) for row in A]


def _norm(v):
    return math.sqrt(sum(x * x for x in v))


def _fix_sign(v):
    k = max(range(len(v)), key=lambda i: abs(v[i]))
    return [-x for x in v] if v[k] < 0 else v


def rayleigh_quotient(A, v):
    """Отношение Рэлея v^T A v / v^T v - оценка собственного значения для вектора v.

    ValueError, если v - нулевой вектор. Входные данные не меняйте.
    """
    vv = sum(x * x for x in v)
    if vv == 0:
        raise ValueError("отношение Рэлея не определено для нулевого вектора")
    Av = _matvec(A, v)
    return sum(a * b for a, b in zip(v, Av)) / vv


def power_iteration(A, num_iters=1000, tol=1e-10):
    """Степенной метод: (lam, v) - доминирующее по модулю собственное значение и его вектор.

    Старт: v = [1.0] * n, нормированный. Шаг: w = A v; если ||w|| == 0 - ValueError;
    v_new = w / ||w||; затем знак фиксируется так, чтобы компонента с наибольшим модулем
    (при равенстве - с меньшим индексом) была положительной. Остановка, когда
    ||v_new - v|| < tol или после num_iters шагов. lam = rayleigh_quotient(A, v).
    Возвращаемый v имеет длину 1.
    """
    n = len(A)
    v = [1.0 / math.sqrt(n)] * n
    for _ in range(num_iters):
        w = _matvec(A, v)
        length = _norm(w)
        if length == 0:
            raise ValueError("A v = 0: степенной метод не определён")
        v_new = _fix_sign([x / length for x in w])
        done = _norm([a - b for a, b in zip(v_new, v)]) < tol
        v = v_new
        if done:
            break
    return rayleigh_quotient(A, v), v


def is_eigenpair(A, lam, v, tol=1e-8):
    """True, если v - собственный вектор A со значением lam.

    Условие: v не нулевой и ||A v - lam v|| <= tol * max(1, |lam|) * ||v||.
    """
    length = _norm(v)
    if length == 0:
        return False
    Av = _matvec(A, v)
    diff = _norm([a - lam * x for a, x in zip(Av, v)])
    return diff <= tol * max(1.0, abs(lam)) * length


def stationary_distribution(P, num_iters=1000, tol=1e-12):
    """Стационарное распределение p (p = p P, sum(p) = 1) марковской цепи со стохастической по строкам P.

    Старт - равномерное распределение; шаг p <- p P (без нормировки по L2, сумма остаётся 1).
    Остановка, когда наибольшее изменение компоненты меньше tol, или после num_iters шагов.
    """
    n = len(P)
    p = [1.0 / n] * n
    for _ in range(num_iters):
        p_new = [sum(p[i] * P[i][j] for i in range(n)) for j in range(n)]
        change = max(abs(a - b) for a, b in zip(p_new, p))
        p = p_new
        if change < tol:
            break
    return p
