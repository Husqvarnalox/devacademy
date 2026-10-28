def _dims(M):
    """Проверяет, что M - непустая прямоугольная матрица, и возвращает (строки, столбцы)."""
    if len(M) == 0 or len(M[0]) == 0:
        raise ValueError("матрица не может быть пустой")
    cols = len(M[0])
    if any(len(row) != cols for row in M):
        raise ValueError("строки матрицы имеют разную длину")
    return len(M), cols


def matmul(A, B):
    """Произведение матриц A (m x n) и B (n x p): новый список из m строк по p чисел.

    C[i][j] = A[i][0] * B[0][j] + ... + A[i][n-1] * B[n-1][j].
    ValueError, если число столбцов A не равно числу строк B (а также для пустых или рваных матриц).
    """
    m, n = _dims(A)
    n2, p = _dims(B)
    if n != n2:
        raise ValueError(f"в A {n} столбцов, а в B {n2} строк")
    C = [[0.0] * p for _ in range(m)]
    for i in range(m):
        for j in range(p):
            for k in range(n):
                C[i][j] += A[i][k] * B[k][j]
    return C


def identity(n):
    """Единичная матрица n x n из чисел float (единицы на главной диагонали). ValueError при n < 1."""
    if n < 1:
        raise ValueError("размер единичной матрицы должен быть не меньше 1")
    return [[1.0 if i == j else 0.0 for j in range(n)] for i in range(n)]


def matrix_power(A, k):
    """Степень A в k-й степени для квадратной матрицы A и целого k >= 0 (k = 0 даёт единичную матрицу).

    ValueError, если A не квадратная или k < 0.
    """
    rows, cols = _dims(A)
    if rows != cols:
        raise ValueError("степень определена только для квадратной матрицы")
    if k < 0:
        raise ValueError("показатель степени должен быть неотрицательным")
    result = identity(rows)
    base = [[float(v) for v in row] for row in A]
    while k > 0:                      # быстрое возведение в степень
        if k % 2 == 1:
            result = matmul(result, base)
        base = matmul(base, base)
        k //= 2
    return result


def compose_linear_layers(Ws):
    """Одна матрица, равная применению слоёв Ws[0], затем Ws[1], ... к вектору: Ws[-1] ... Ws[1] Ws[0].

    Ws - непустой список матриц, у Ws[i + 1] число столбцов равно числу строк Ws[i].
    ValueError для пустого списка и несовместимых форм.
    """
    if len(Ws) == 0:
        raise ValueError("нужен хотя бы один слой")
    _dims(Ws[0])
    result = [[float(v) for v in row] for row in Ws[0]]
    for W in Ws[1:]:
        result = matmul(W, result)
    return result
