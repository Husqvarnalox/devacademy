import math


def top_singular_triplet(A, num_iters=1000, tol=1e-12):
    """Главная сингулярная тройка (sigma, u, v) матрицы A (m строк, n столбцов): A v = sigma u.

    Степенной метод для A^T A: старт v - строка A с наибольшей длиной (первая при равенстве),
    нормированная; шаг w = A^T (A v);
    если ||w|| == 0 - ValueError; v <- w / ||w|| и фиксация знака (наибольшая по модулю
    компонента положительна, при равенстве - с меньшим индексом). Остановка, когда
    ||v_new - v|| < tol или после num_iters шагов. Затем sigma = ||A v|| и u = A v / sigma.
    ValueError, если A - нулевая матрица.
    """
    # TODO
    return 0.0, [0.0] * len(A), [0.0] * len(A[0])


def rank_one_approximation(A):
    """Лучшее приближение ранга 1: матрица sigma * u v^T (список строк той же формы, что A).

    ValueError для нулевой матрицы (из top_singular_triplet).
    """
    # TODO
    return [[0.0] * len(A[0]) for _ in A]


def first_principal_component(X):
    """Первая главная компонента данных X (список объектов-строк): кортеж (mean, component).

    mean - среднее по каждому столбцу; component - главный правый сингулярный вектор
    центрированных данных (длина 1, знак зафиксирован как в top_singular_triplet).
    ValueError, если в X меньше двух строк.
    """
    # TODO
    d = len(X[0])
    return [0.0] * d, [0.0] * d


def project(X, mean, component):
    """Координаты объектов вдоль компоненты: для каждой строки x число (x - mean) . component."""
    # TODO
    return []
