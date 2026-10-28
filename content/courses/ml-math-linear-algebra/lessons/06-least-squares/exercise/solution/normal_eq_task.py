def transpose(A):
    """Транспонирование матрицы (списка строк): строки становятся столбцами. Готовая функция."""
    return [list(col) for col in zip(*A)]


def matmul(A, B):
    """Произведение матриц A (m x k) и B (k x n) - новый список строк. Готовая функция."""
    Bt = transpose(B)
    return [[sum(a * b for a, b in zip(row, col)) for col in Bt] for row in A]


def solve(A, b):
    """Решает систему A x = b методом Гаусса с выбором главного элемента по столбцу. Готовая функция.

    A - квадратная матрица (список строк), b - список правых частей. Входные данные не меняются.
    Если главный элемент по модулю меньше 1e-12 (матрица вырождена), выбрасывает ValueError.
    """
    n = len(A)
    M = [[float(x) for x in row] + [float(b[i])] for i, row in enumerate(A)]
    for col in range(n):
        pivot = max(range(col, n), key=lambda r: abs(M[r][col]))
        if abs(M[pivot][col]) < 1e-12:
            raise ValueError("матрица вырождена: систему нельзя решить однозначно")
        M[col], M[pivot] = M[pivot], M[col]
        for r in range(col + 1, n):
            factor = M[r][col] / M[col][col]
            for c in range(col, n + 1):
                M[r][c] -= factor * M[col][c]
    x = [0.0] * n
    for i in range(n - 1, -1, -1):
        s = sum(M[i][j] * x[j] for j in range(i + 1, n))
        x[i] = (M[i][n] - s) / M[i][i]
    return x


def add_bias_column(X):
    """Новая матрица, в которой к каждой строке спереди добавлена единица: [1.0] + row.

    Исходную X не меняйте.
    """
    return [[1.0] + list(row) for row in X]


def fit_normal_equation(X, y, l2=0.0):
    """Веса линейной регрессии w (длина d + 1, w[0] - свободный член) из нормальных уравнений.

    Решает (Xb^T Xb + l2 * I') w = Xb^T y, где Xb = add_bias_column(X), а I' - единичная
    матрица с нулём на месте свободного члена (его не штрафуют).
    Если система вырождена, ValueError из solve не перехватывайте.
    """
    Xb = add_bias_column(X)
    Xt = transpose(Xb)
    A = matmul(Xt, Xb)
    for i in range(1, len(A)):
        A[i][i] += l2
    rhs = [sum(a * b for a, b in zip(row, y)) for row in Xt]
    return solve(A, rhs)


def predict(X, w):
    """Предсказания модели: для каждой строки x значение w[0] + w[1] * x[0] + w[2] * x[1] + ..."""
    return [w[0] + sum(wi * xi for wi, xi in zip(w[1:], row)) for row in X]


def mse(y_true, y_pred):
    """Средний квадрат ошибки: сумма (y_true[i] - y_pred[i]) ** 2, делённая на число объектов."""
    return sum((a - b) ** 2 for a, b in zip(y_true, y_pred)) / len(y_true)
