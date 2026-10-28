import numpy as np


def solve_many(A, B):
    """Решить A X = B сразу для нескольких правых частей: A - (n, n), B - (n, k), столбцы B - отдельные правые части.

    Верните X формы (n, k) с помощью np.linalg.solve. Для вырожденной A допускается numpy.linalg.LinAlgError.
    """
    return np.linalg.solve(np.asarray(A, dtype=float), np.asarray(B, dtype=float))


def relative_residual(A, x, b):
    """Относительная невязка ||A x - b|| / ||b|| (норма L2). Если ||b|| == 0, верните ||A x||."""
    A = np.asarray(A, dtype=float)
    x = np.asarray(x, dtype=float)
    b = np.asarray(b, dtype=float)
    residual = np.linalg.norm(A @ x - b)
    b_norm = np.linalg.norm(b)
    if b_norm == 0.0:
        return float(np.linalg.norm(A @ x))
    return float(residual / b_norm)


def perturbation_ratio(A, b, db):
    """Во сколько раз относительная ошибка решения больше относительной ошибки правой части.

    x = solve(A, b), x + dx = solve(A, b + db). Верните (||dx|| / ||x||) / (||db|| / ||b||).
    Считайте, что b и db ненулевые.
    """
    A = np.asarray(A, dtype=float)
    b = np.asarray(b, dtype=float)
    db = np.asarray(db, dtype=float)
    x = np.linalg.solve(A, b)
    dx = np.linalg.solve(A, b + db) - x
    return float((np.linalg.norm(dx) / np.linalg.norm(x)) / (np.linalg.norm(db) / np.linalg.norm(b)))
