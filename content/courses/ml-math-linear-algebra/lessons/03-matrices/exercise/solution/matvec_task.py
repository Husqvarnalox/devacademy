def shape(A):
    """Форма матрицы (число строк, число столбцов) для списка строк A.

    ValueError, если A пуста (A == []), если есть строка нулевой длины
    или если строки имеют разную длину.
    """
    if len(A) == 0:
        raise ValueError("матрица не может быть пустой")
    cols = len(A[0])
    if cols == 0:
        raise ValueError("строка матрицы не может быть пустой")
    for row in A:
        if len(row) != cols:
            raise ValueError("строки матрицы имеют разную длину")
    return (len(A), cols)


def transpose(A):
    """Транспонированная матрица: новый список списков, строки и столбцы поменяны местами."""
    rows, cols = shape(A)
    return [[A[i][j] for i in range(rows)] for j in range(cols)]


def matvec(A, x):
    """Произведение матрицы A на вектор x: список, i-й элемент - скалярное произведение i-й строки A на x.

    ValueError, если число столбцов A не равно len(x) (или A не является матрицей).
    """
    rows, cols = shape(A)
    if cols != len(x):
        raise ValueError(f"в матрице {cols} столбцов, а длина вектора {len(x)}")
    return [sum(a * v for a, v in zip(row, x)) for row in A]


def dense_layer(W, b, x, activation=None):
    """Слой нейронной сети: W x + b, затем, если activation == "relu", max(0, z) для каждой компоненты.

    activation=None - без активации. Любая другая строка - ValueError.
    ValueError также, если len(b) не равно числу строк W или размеры W и x не согласованы.
    """
    if activation not in (None, "relu"):
        raise ValueError(f"неизвестная активация: {activation!r}")
    rows, _ = shape(W)
    if len(b) != rows:
        raise ValueError(f"в W {rows} строк, а длина b {len(b)}")
    z = [s + bi for s, bi in zip(matvec(W, x), b)]
    if activation == "relu":
        z = [max(0.0, v) for v in z]
    return z
