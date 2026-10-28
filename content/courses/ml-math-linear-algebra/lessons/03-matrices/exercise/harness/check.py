import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import matvec_task as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() возвращает None при успехе или строку с причиной провала; исключения - провал."""
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def close(a, b, tol=1e-9):
    return isinstance(a, list) and len(a) == len(b) and all(abs(x - y) <= tol for x, y in zip(a, b))


def close_matrix(a, b, tol=1e-9):
    return isinstance(a, list) and len(a) == len(b) and all(close(r, s, tol) for r, s in zip(a, b))


def raises_value_error(fn, *args):
    try:
        fn(*args)
    except ValueError:
        return True
    except Exception:
        return False
    return False


def t_shape_transpose():
    A = [[1.0, 2.0, 0.0], [0.0, -1.0, 3.0]]
    got = m.shape(A)
    if tuple(got) != (2, 3):
        return f"shape для матрицы 2 x 3 должна вернуть (2, 3), получили {got!r}"
    got = m.transpose(A)
    if not close_matrix(got, [[1.0, 0.0], [2.0, -1.0], [0.0, 3.0]]):
        return f"транспонированная матрица 3 x 2: [[1, 0], [2, -1], [0, 3]], получили {got!r}"


def t_matvec():
    A = [[1.0, 2.0, 0.0], [0.0, -1.0, 3.0]]
    got = m.matvec(A, [4.0, 1.0, 2.0])
    if not close(got, [6.0, 5.0]):
        return f"A x для A = [[1, 2, 0], [0, -1, 3]], x = [4, 1, 2] равно [6, 5], получили {got!r}"
    got = m.matvec([[1.0, 0.0], [0.0, 1.0]], [7.0, -3.0])
    if not close(got, [7.0, -3.0]):
        return f"единичная матрица не меняет вектор, получили {got!r}"
    got = m.matvec([[0.0, -1.0], [1.0, 0.0]], [1.0, 0.0])
    if not close(got, [0.0, 1.0]):
        return f"поворот на 90 градусов переводит (1, 0) в (0, 1), получили {got!r}"


def t_dense_layer():
    W = [[1.0, -1.0, 0.5], [0.0, 2.0, -1.0]]
    b = [0.5, -1.0]
    got = m.dense_layer(W, b, [4.0, 0.0, 1.0])
    if not close(got, [5.0, -2.0]):
        return f"W x + b без активации для x = [4, 0, 1] равно [5, -2], получили {got!r}"
    got = m.dense_layer(W, b, [4.0, 0.0, 1.0], "relu")
    if not close(got, [5.0, 0.0]):
        return f"после ReLU отрицательные значения обнуляются: [5, 0], получили {got!r}"
    got = m.dense_layer(W, b, [1.0, 2.0, 3.0], activation="relu")
    if not close(got, [1.0, 0.0]):
        return f"ReLU для x = [1, 2, 3]: [1, 0], получили {got!r}"


def t_shape_errors():
    if not raises_value_error(m.shape, []):
        return "shape([]) должна выбросить ValueError"
    if not raises_value_error(m.shape, [[]]):
        return "shape([[]]) - строка нулевой длины - должна выбросить ValueError"
    if not raises_value_error(m.shape, [[1.0, 2.0], [3.0]]):
        return "shape для строк разной длины должна выбросить ValueError"
    if not raises_value_error(m.shape, [[1.0], [2.0, 3.0]]):
        return "shape: вторая строка длиннее первой - тоже ValueError"
    if not raises_value_error(m.transpose, [[1.0, 2.0], [3.0]]):
        return "transpose для рваной матрицы должна выбросить ValueError"


def t_size_errors():
    A = [[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]
    if not raises_value_error(m.matvec, A, [1.0, 2.0]):
        return "matvec: 3 столбца и вектор длины 2 - ValueError"
    if not raises_value_error(m.matvec, A, [1.0, 2.0, 3.0, 4.0]):
        return "matvec: 3 столбца и вектор длины 4 - ValueError"
    if not raises_value_error(m.dense_layer, A, [0.0], [1.0, 2.0, 3.0]):
        return "dense_layer: в W две строки, а b длины 1 - ValueError"
    if not raises_value_error(m.dense_layer, A, [0.0, 0.0], [1.0, 2.0]):
        return "dense_layer: размеры W и x не согласованы - ValueError"
    if not raises_value_error(m.dense_layer, A, [0.0, 0.0], [1.0, 2.0, 3.0], "sigmoid"):
        return "dense_layer с неизвестной активацией должна выбросить ValueError"
    if not raises_value_error(m.dense_layer, A, [0.0, 0.0], [1.0, 2.0, 3.0], "ReLU"):
        return "названия активаций различают регистр: 'ReLU' - неизвестная активация"


def t_no_mutation():
    A = [[1.0, 2.0], [3.0, 4.0]]
    x = [1.0, -1.0]
    b = [0.0, 10.0]
    T = m.transpose(A)
    y = m.matvec(A, x)
    z = m.dense_layer(A, b, x, "relu")
    if A != [[1.0, 2.0], [3.0, 4.0]] or x != [1.0, -1.0] or b != [0.0, 10.0]:
        return "функции не должны изменять входные списки"
    T[0][0] = 99.0
    if A[0][0] != 1.0:
        return "transpose должна возвращать новые списки, а не ссылки на строки исходной матрицы"
    if y is x or z is x or z is b:
        return "результат должен быть новым списком"


def t_edge_cases():
    if tuple(m.shape([[5.0]])) != (1, 1):
        return "форма матрицы 1 x 1 - (1, 1)"
    if tuple(m.shape([[1.0, 2.0, 3.0]])) != (1, 3):
        return "[[1, 2, 3]] - одна строка, форма (1, 3)"
    if tuple(m.shape([[1.0], [2.0], [3.0]])) != (3, 1):
        return "[[1], [2], [3]] - один столбец, форма (3, 1)"
    if not close_matrix(m.transpose([[1.0, 2.0, 3.0]]), [[1.0], [2.0], [3.0]]):
        return "транспонированная строка - столбец"
    A = [[1.0, 2.0], [3.0, 4.0], [5.0, 6.0]]
    if not close_matrix(m.transpose(m.transpose(A)), A):
        return "двойное транспонирование возвращает исходную матрицу"
    if not close(m.matvec([[3.0]], [2.0]), [6.0]):
        return "матрица 1 x 1 на вектор длины 1"
    if not close(m.matvec([[1, 2], [3, 4], [5, 6]], [1, 1]), [3.0, 7.0, 11.0]):
        return "matvec для матрицы 3 x 2 (высокая матрица) и целых чисел: [3, 7, 11]"
    if not close(m.matvec([[1.0, 2.0, 3.0]], [0.0, 0.0, 0.0]), [0.0]):
        return "матрица на нулевой вектор даёт нулевой вектор"
    got = m.dense_layer([[1.0, 1.0]], [-2.0], [1.0, 1.0], "relu")
    if not close(got, [0.0]):
        return f"ReLU(0) = 0: W x + b = 0, получили {got!r}"
    got = m.dense_layer([[1.0, 1.0]], [-5.0], [1.0, 1.0])
    if not close(got, [-3.0]):
        return f"без активации отрицательные значения остаются: [-3], получили {got!r}"
    got = m.dense_layer([[2, 0], [0, 3]], [1, 1], [1, 2], None)
    if not close(got, [3.0, 7.0]):
        return f"dense_layer с целыми числами: [3, 7], получили {got!r}"


case("shape-transpose", t_shape_transpose)
case("matvec", t_matvec)
case("dense-layer", t_dense_layer)
case("shape-errors", t_shape_errors)
case("size-errors", t_size_errors)
case("no-mutation", t_no_mutation)
case("edge-cases", t_edge_cases)
