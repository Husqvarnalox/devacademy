import math


def _norm(v):
    return math.sqrt(sum(x * x for x in v))


def _matvec(A, v):
    return [sum(a * x for a, x in zip(row, v)) for row in A]


def _rmatvec(A, u):
    """Произведение A^T u без построения транспонированной матрицы."""
    return [sum(A[i][j] * u[i] for i in range(len(A))) for j in range(len(A[0]))]


def _fix_sign(v):
    k = max(range(len(v)), key=lambda i: abs(v[i]))
    return [-x for x in v] if v[k] < 0 else v


def top_singular_triplet(A, num_iters=1000, tol=1e-12):
    """Главная сингулярная тройка (sigma, u, v) матрицы A (m строк, n столбцов): A v = sigma u.

    Степенной метод для A^T A: старт v - строка A с наибольшей длиной (первая при равенстве),
    нормированная; шаг w = A^T (A v);
    если ||w|| == 0 - ValueError; v <- w / ||w|| и фиксация знака (наибольшая по модулю
    компонента положительна, при равенстве - с меньшим индексом). Остановка, когда
    ||v_new - v|| < tol или после num_iters шагов. Затем sigma = ||A v|| и u = A v / sigma.
    ValueError, если A - нулевая матрица.
    """
    start = max(A, key=_norm)
    if _norm(start) == 0:
        raise ValueError("A - нулевая матрица: сингулярных векторов с sigma > 0 нет")
    v = [x / _norm(start) for x in start]
    for _ in range(num_iters):
        w = _rmatvec(A, _matvec(A, v))
        length = _norm(w)
        if length == 0:
            raise ValueError("A^T A v = 0: главную сингулярную тройку найти нельзя")
        v_new = _fix_sign([x / length for x in w])
        done = _norm([a - b for a, b in zip(v_new, v)]) < tol
        v = v_new
        if done:
            break
    Av = _matvec(A, v)
    sigma = _norm(Av)
    if sigma == 0:
        raise ValueError("A v = 0: главную сингулярную тройку найти нельзя")
    return sigma, [x / sigma for x in Av], v


def rank_one_approximation(A):
    """Лучшее приближение ранга 1: матрица sigma * u v^T (список строк той же формы, что A).

    ValueError для нулевой матрицы (из top_singular_triplet).
    """
    sigma, u, v = top_singular_triplet(A)
    return [[sigma * ui * vj for vj in v] for ui in u]


def first_principal_component(X):
    """Первая главная компонента данных X (список объектов-строк): кортеж (mean, component).

    mean - среднее по каждому столбцу; component - главный правый сингулярный вектор
    центрированных данных (длина 1, знак зафиксирован как в top_singular_triplet).
    ValueError, если в X меньше двух строк.
    """
    n = len(X)
    if n < 2:
        raise ValueError("для главной компоненты нужно не меньше двух объектов")
    d = len(X[0])
    mean = [sum(row[j] for row in X) / n for j in range(d)]
    centered = [[row[j] - mean[j] for j in range(d)] for row in X]
    _, _, v = top_singular_triplet(centered)
    return mean, v


def project(X, mean, component):
    """Координаты объектов вдоль компоненты: для каждой строки x число (x - mean) . component."""
    return [sum((xj - mj) * cj for xj, mj, cj in zip(row, mean, component)) for row in X]
