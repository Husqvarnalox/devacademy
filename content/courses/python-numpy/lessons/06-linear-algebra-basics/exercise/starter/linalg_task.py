import numpy as np


def matmul_shape(s1, s2):
    """Форма результата a @ b для форм s1 и s2 (1D или 2D) БЕЗ вызова matmul.

    1D @ 1D -> (); (n,) @ (n, k) -> (k,); (m, n) @ (n,) -> (m,); (m, n) @ (n, k) -> (m, k).
    При несогласованных внутренних размерах бросьте ValueError.
    """
    # TODO
    return ()


def affine(X, W, b):
    """Аффинное преобразование X @ W + b: X (n, d), W (d, k), b (k,). Результат float64 формы (n, k)."""
    # TODO
    return X


def solve_system(A, b):
    """Решите A x = b для квадратной A. Используйте np.linalg.solve, а НЕ вычисление обратной матрицы.

    b может быть вектором (n,) или матрицей (n, k). Для вырожденной A пусть np.linalg.LinAlgError пробрасывается.
    """
    # TODO
    return b


def row_norms(a, ord=2):
    """Нормы строк 2D-массива a (ord: 1, 2 или np.inf): float64, форма (число строк,)."""
    # TODO
    return a


def normalize_rows(a):
    """Поделите каждую строку 2D-массива на её L2-норму; нулевые строки остаются нулевыми. float64, та же форма."""
    # TODO
    return a
