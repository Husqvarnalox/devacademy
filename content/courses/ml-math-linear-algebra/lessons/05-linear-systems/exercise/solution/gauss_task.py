EPS = 1e-12


def _check_square(A):
    n = len(A)
    if n == 0 or any(len(row) != n for row in A):
        raise ValueError("нужна непустая квадратная матрица")
    return n


def solve(A, b):
    """Решение системы A x = b методом Гаусса с частичным выбором ведущего элемента.

    A - квадратная матрица (список строк), b - список правых частей. Входные данные не изменяйте: работайте с копиями.
    ValueError, если A не квадратная, len(b) не равна её размеру или матрица вырождена
    (наибольший по модулю кандидат в ведущие элементы меньше 1e-12).
    """
    n = _check_square(A)
    if len(b) != n:
        raise ValueError(f"размер A {n}, а длина b {len(b)}")
    M = [[float(v) for v in row] + [float(bi)] for row, bi in zip(A, b)]
    for col in range(n):
        pivot = max(range(col, n), key=lambda r: abs(M[r][col]))
        if abs(M[pivot][col]) < EPS:
            raise ValueError("матрица вырождена")
        M[col], M[pivot] = M[pivot], M[col]
        for r in range(col + 1, n):
            factor = M[r][col] / M[col][col]
            M[r] = [a - factor * c for a, c in zip(M[r], M[col])]
    x = [0.0] * n
    for i in range(n - 1, -1, -1):
        s = sum(M[i][j] * x[j] for j in range(i + 1, n))
        x[i] = (M[i][n] - s) / M[i][i]
    return x


def determinant(A):
    """Определитель квадратной матрицы: произведение ведущих элементов со знаком от перестановок строк.

    Для вырожденной матрицы верните 0.0 (это не ошибка). ValueError, если A не квадратная.
    """
    n = _check_square(A)
    M = [[float(v) for v in row] for row in A]
    det = 1.0
    for col in range(n):
        pivot = max(range(col, n), key=lambda r: abs(M[r][col]))
        if abs(M[pivot][col]) < EPS:
            return 0.0
        if pivot != col:
            M[col], M[pivot] = M[pivot], M[col]
            det = -det
        det *= M[col][col]
        for r in range(col + 1, n):
            factor = M[r][col] / M[col][col]
            M[r] = [a - factor * c for a, c in zip(M[r], M[col])]
    return det


def inverse(A):
    """Обратная матрица (список списков) методом Гаусса - Жордана для расширенной матрицы [A | I].

    ValueError, если A не квадратная или вырождена (ведущий кандидат по модулю меньше 1e-12).
    """
    n = _check_square(A)
    M = [[float(v) for v in row] + [1.0 if i == j else 0.0 for j in range(n)] for i, row in enumerate(A)]
    for col in range(n):
        pivot = max(range(col, n), key=lambda r: abs(M[r][col]))
        if abs(M[pivot][col]) < EPS:
            raise ValueError("матрица вырождена")
        M[col], M[pivot] = M[pivot], M[col]
        lead = M[col][col]
        M[col] = [v / lead for v in M[col]]
        for r in range(n):
            if r != col:
                factor = M[r][col]
                M[r] = [a - factor * c for a, c in zip(M[r], M[col])]
    return [row[n:] for row in M]
