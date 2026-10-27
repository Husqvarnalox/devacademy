import numpy as np


def solve_many(A, B):
    """Решить A X = B сразу для нескольких правых частей: A - (n, n), B - (n, k), столбцы B - отдельные правые части.

    Верните X формы (n, k) с помощью np.linalg.solve. Для вырожденной A допускается numpy.linalg.LinAlgError.
    """
    # TODO
    return np.zeros_like(B, dtype=float)


def relative_residual(A, x, b):
    """Относительная невязка ||A x - b|| / ||b|| (норма L2). Если ||b|| == 0, верните ||A x||."""
    # TODO
    return 0.0


def perturbation_ratio(A, b, db):
    """Во сколько раз относительная ошибка решения больше относительной ошибки правой части.

    x = solve(A, b), x + dx = solve(A, b + db). Верните (||dx|| / ||x||) / (||db|| / ||b||).
    Считайте, что b и db ненулевые.
    """
    # TODO
    return 0.0
