import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import network_task as m


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


def raises_value_error(fn, *args):
    try:
        fn(*args)
    except ValueError:
        return True
    except Exception:
        return False
    return False


def forward(Ws, bs, x):
    for W, b in zip(Ws, bs):
        x = W @ x + b
    return x


def t_collapse_basic():
    W1 = np.array([[1.0, -1.0, 2.0], [0.5, 0.0, 1.0]])
    b1 = np.array([1.0, -1.0])
    W2 = np.array([[2.0, 1.0], [0.0, -1.0], [1.0, 3.0]])
    b2 = np.array([0.0, 0.5, -2.0])
    W, b = m.collapse_affine_layers([W1, W2], [b1, b2])
    wantW = W2 @ W1
    wantb = W2 @ b1 + b2
    if W.shape != (3, 3) or b.shape != (3,):
        return f"W должна иметь форму (3, 3), b - (3,); получили {W.shape} и {b.shape}"
    if not np.allclose(W, wantW, rtol=0, atol=1e-12):
        return f"матрица свёрнутого слоя W2 W1 = {wantW.tolist()}, получили {W.tolist()}"
    if not np.allclose(b, wantb, rtol=0, atol=1e-12):
        return f"сдвиг свёрнутого слоя W2 b1 + b2 = {wantb.tolist()}, получили {b.tolist()}"


def t_collapse_random():
    rng = np.random.default_rng(5)
    dims = [4, 6, 3, 5, 2]
    Ws = [rng.normal(size=(dims[i + 1], dims[i])) for i in range(len(dims) - 1)]
    bs = [rng.normal(size=dims[i + 1]) for i in range(len(dims) - 1)]
    W, b = m.collapse_affine_layers(Ws, bs)
    if W.shape != (2, 4) or b.shape != (2,):
        return f"4 слоя 4 -> 6 -> 3 -> 5 -> 2: W (2, 4), b (2,); получили {W.shape} и {b.shape}"
    for _ in range(5):
        x = rng.normal(size=4)
        if not np.allclose(W @ x + b, forward(Ws, bs, x), rtol=0, atol=1e-9):
            return "свёрнутый слой должен давать те же выходы, что и цепочка слоёв"


def t_markov_basic():
    P = np.array([[0.9, 0.1], [0.5, 0.5]])
    got = m.markov_distribution(P, np.array([0.0, 1.0]), 3)
    if not np.allclose(got, [0.78, 0.22], rtol=0, atol=1e-12):
        return f"погода через 3 дня после дождя: [0.78, 0.22], получили {got.tolist()}"
    got = m.markov_distribution(P, np.array([1.0, 0.0]), 1)
    if not np.allclose(got, [0.9, 0.1], rtol=0, atol=1e-12):
        return f"через 1 шаг из состояния 0: первая строка P = [0.9, 0.1], получили {got.tolist()}"


def t_markov_properties():
    rng = np.random.default_rng(21)
    raw = rng.random(size=(5, 5)) + 0.05
    P = raw / raw.sum(axis=1, keepdims=True)
    p0 = np.array([1.0, 0.0, 0.0, 0.0, 0.0])
    got0 = m.markov_distribution(P, p0, 0)
    if not np.allclose(got0, p0, rtol=0, atol=1e-12):
        return "при steps = 0 распределение не меняется"
    for steps in (1, 2, 7, 40):
        got = m.markov_distribution(P, p0, steps)
        if got.shape != (5,) or abs(got.sum() - 1.0) > 1e-9 or np.any(got < -1e-12):
            return f"после {steps} шагов вероятности неотрицательны и суммируются в 1"
    ref = p0.copy()
    for _ in range(7):
        ref = ref @ P
    if not np.allclose(m.markov_distribution(P, p0, 7), ref, rtol=0, atol=1e-12):
        return "результат должен совпадать с семью последовательными умножениями p @ P"
    # стационарное распределение: p = p @ P
    far = m.markov_distribution(P, p0, 200)
    if not np.allclose(far, far @ P, rtol=0, atol=1e-9):
        return "через много шагов распределение стационарно: p = p @ P"
    other = m.markov_distribution(P, np.full(5, 0.2), 200)
    if not np.allclose(far, other, rtol=0, atol=1e-9):
        return "для цепи с положительными вероятностями стационарное распределение не зависит от старта"


def t_validation():
    P = np.array([[0.9, 0.1], [0.5, 0.5]])
    p0 = np.array([0.0, 1.0])
    if not raises_value_error(m.markov_distribution, np.array([[0.9, 0.2], [0.5, 0.5]]), p0, 2):
        return "строка P с суммой 1.1 - ValueError"
    if not raises_value_error(m.markov_distribution, P, np.array([0.5, 0.6]), 2):
        return "p0 с суммой 1.1 - ValueError"
    if not raises_value_error(m.markov_distribution, np.array([[0.5, 0.5, 0.0], [1.0, 0.0, 0.0]]), p0, 2):
        return "P не квадратная - ValueError"
    if not raises_value_error(m.markov_distribution, P, np.array([1.0, 0.0, 0.0]), 2):
        return "длина p0 не совпадает с размером P - ValueError"
    if not raises_value_error(m.markov_distribution, P, p0, -1):
        return "отрицательное число шагов - ValueError"
    ok = m.markov_distribution(np.array([[0.1, 0.9], [0.5, 0.5 + 1e-12]]), p0, 1)
    if not np.all(np.isfinite(ok)):
        return "отклонение суммы строки от 1 в пределах 1e-9 допустимо"
    W = np.ones((2, 3))
    if not raises_value_error(m.collapse_affine_layers, [], []):
        return "collapse_affine_layers с пустыми списками - ValueError"
    if not raises_value_error(m.collapse_affine_layers, [W], [np.zeros(2), np.zeros(2)]):
        return "разное число матриц и сдвигов - ValueError"
    if not raises_value_error(m.collapse_affine_layers, [W, W], [np.zeros(2), np.zeros(2)]):
        return "второй слой ждёт 3 входа, а первый даёт 2 выхода - ValueError"
    if not raises_value_error(m.collapse_affine_layers, [W], [np.zeros(3)]):
        return "длина сдвига не равна числу выходов слоя - ValueError"


def t_int_no_mutation():
    Ws = [np.array([[1, 2], [3, 4]]), np.array([[0, 1], [1, 0]])]
    bs = [np.array([1, 1]), np.array([0, 2])]
    Ws0 = [w.copy() for w in Ws]
    bs0 = [b.copy() for b in bs]
    W, b = m.collapse_affine_layers(Ws, bs)
    if any(not np.array_equal(a, c) for a, c in zip(Ws, Ws0)) or any(not np.array_equal(a, c) for a, c in zip(bs, bs0)):
        return "collapse_affine_layers не должна менять входные массивы"
    if W.dtype.kind != "f" or b.dtype.kind != "f":
        return f"результат должен быть типа float, получили {W.dtype} и {b.dtype}"
    if not np.allclose(W, [[3.0, 4.0], [1.0, 2.0]]) or not np.allclose(b, [1.0, 3.0]):
        return f"[[0, 1], [1, 0]] (W1 x + b1) + b2: ожидали W = [[3, 4], [1, 2]], b = [1, 3]; получили {W.tolist()} и {b.tolist()}"
    W1, b1 = m.collapse_affine_layers([Ws[0]], [bs[0]])
    W1[0, 0] = 100.0
    b1[0] = 100.0
    if Ws[0][0, 0] != 1 or bs[0][0] != 1:
        return "для одного слоя нужно вернуть копии, а не входные массивы"
    P = np.array([[0.5, 0.5], [0.25, 0.75]])
    p0 = np.array([1.0, 0.0])
    P0, p00 = P.copy(), p0.copy()
    r = m.markov_distribution(P, p0, 0)
    r[0] = 7.0
    m.markov_distribution(P, p0, 3)
    if not (np.array_equal(P, P0) and np.array_equal(p0, p00)):
        return "markov_distribution не должна менять P и p0 (при steps = 0 верните копию p0)"
    got = m.markov_distribution(np.array([[1, 0], [0, 1]]), np.array([1, 0]), 5)
    if got.dtype.kind != "f" or not np.allclose(got, [1.0, 0.0]):
        return "целочисленный вход допустим: единичная матрица переходов не меняет распределение"


case("collapse-basic", t_collapse_basic)
case("collapse-random", t_collapse_random)
case("markov-basic", t_markov_basic)
case("markov-properties", t_markov_properties)
case("validation", t_validation)
case("int-no-mutation", t_int_no_mutation)
