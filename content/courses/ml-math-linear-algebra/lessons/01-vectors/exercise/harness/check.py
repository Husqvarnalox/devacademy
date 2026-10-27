import json
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import vectors_task as m


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


def raises_value_error(fn, *args):
    try:
        fn(*args)
    except ValueError:
        return True
    except Exception:
        return False
    return False


def t_add_scale():
    got = m.add([1.0, 2.0, 3.0], [10.0, 20.0, 30.0])
    if not close(got, [11.0, 22.0, 33.0]):
        return f"add([1, 2, 3], [10, 20, 30]) должна вернуть [11, 22, 33], получили {got!r}"
    got = m.scale(2.5, [1.0, -2.0, 0.0])
    if not close(got, [2.5, -5.0, 0.0]):
        return f"scale(2.5, [1, -2, 0]) должна вернуть [2.5, -5, 0], получили {got!r}"


def t_linear_combination():
    got = m.linear_combination([2.0, -1.0], [[1.0, 0.0, 3.0], [0.5, 1.0, 1.0]])
    if not close(got, [1.5, -1.0, 5.0]):
        return f"2*[1, 0, 3] - [0.5, 1, 1] = [1.5, -1, 5], получили {got!r}"
    got = m.linear_combination([1.0, -0.1], [[0.5, -1.0], [2.0, -4.0]])
    if not close(got, [0.3, -0.6]):
        return f"шаг w - 0.1*g для w = [0.5, -1], g = [2, -4] равен [0.3, -0.6], получили {got!r}"


def t_mean_vector():
    got = m.mean_vector([[1.0, 2.0], [3.0, 2.0], [2.0, 5.0]])
    if not close(got, [2.0, 3.0]):
        return f"центр точек [1, 2], [3, 2], [2, 5] равен [2, 3], получили {got!r}"


def t_length_mismatch():
    if not raises_value_error(m.add, [1.0, 2.0], [1.0, 2.0, 3.0]):
        return "add для векторов длины 2 и 3 должна выбросить ValueError"
    if not raises_value_error(m.linear_combination, [1.0, 2.0], [[1.0, 2.0], [3.0]]):
        return "linear_combination для векторов разной длины должна выбросить ValueError"
    if not raises_value_error(m.linear_combination, [1.0], [[1.0], [2.0]]):
        return "linear_combination: число коэффициентов не равно числу векторов - ValueError"
    if not raises_value_error(m.linear_combination, [], []):
        return "linear_combination без векторов должна выбросить ValueError"
    if not raises_value_error(m.mean_vector, []):
        return "mean_vector([]) должна выбросить ValueError, а не ZeroDivisionError"
    if not raises_value_error(m.mean_vector, [[1.0, 2.0], [1.0]]):
        return "mean_vector для векторов разной длины должна выбросить ValueError"


def t_no_mutation():
    u, v = [1.0, 2.0], [3.0, 4.0]
    vs = [[1.0, 1.0], [2.0, 0.0]]
    r1 = m.add(u, v)
    r2 = m.scale(3.0, u)
    r3 = m.linear_combination([1.0, 2.0], vs)
    r4 = m.mean_vector(vs)
    if u != [1.0, 2.0] or v != [3.0, 4.0] or vs != [[1.0, 1.0], [2.0, 0.0]]:
        return "функции не должны изменять входные списки"
    if r1 is u or r1 is v or r2 is u or r3 is vs[0] or r4 is vs[0]:
        return "результат должен быть новым списком, а не одним из входных"


def t_edge_cases():
    if not close(m.add([], []), []):
        return "сумма двух пустых векторов - пустой вектор"
    if not close(m.scale(-1.0, [3.0, -2.0]), [-3.0, 2.0]):
        return "умножение на -1 меняет знак каждой компоненты"
    if not close(m.linear_combination([0.0, 0.0], [[5.0, 6.0], [7.0, 8.0]]), [0.0, 0.0]):
        return "линейная комбинация с нулевыми коэффициентами - нулевой вектор"
    if not close(m.linear_combination([4.0], [[1.0, -2.0, 0.5]]), [4.0, -8.0, 2.0]):
        return "линейная комбинация одного вектора равна scale"
    if not close(m.mean_vector([[7.0, -1.0, 2.0]]), [7.0, -1.0, 2.0]):
        return "среднее одного вектора - он сам"
    if not close(m.mean_vector([[1, 2], [2, 3]]), [1.5, 2.5]):
        return "среднее целочисленных векторов [1, 2] и [2, 3] равно [1.5, 2.5]"
    big = [[float(i), float(-i), 1.0] for i in range(1, 101)]
    if not close(m.mean_vector(big), [50.5, -50.5, 1.0], tol=1e-9):
        return "среднее 100 векторов посчитано неверно"


case("add-scale", t_add_scale)
case("linear-combination", t_linear_combination)
case("mean-vector", t_mean_vector)
case("length-mismatch", t_length_mismatch)
case("no-mutation", t_no_mutation)
case("edge-cases", t_edge_cases)
