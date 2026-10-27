import json
import os
import warnings

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import embed_task as m


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


E = np.array([[0.0, 1.0, 2.0], [3.0, 4.0, 5.0], [6.0, 7.0, 8.0], [9.0, 10.0, 11.0]])


def close(a, b, tol=1e-9):
    return np.allclose(np.asarray(a, dtype=np.float64), np.asarray(b, dtype=np.float64), rtol=0, atol=tol)


def t_lookup_basic():
    got = m.lookup(E, [2, 0, 2])
    if not isinstance(got, np.ndarray) or got.dtype != np.float64 or got.shape != (3, 3):
        return f"lookup должна вернуть float64-массив формы (3, 3), получили {getattr(got, 'dtype', None)} {getattr(got, 'shape', None)}"
    if not close(got, [[6, 7, 8], [0, 1, 2], [6, 7, 8]]):
        return f"lookup(E, [2, 0, 2]): строки 2, 0, 2 матрицы E, получили {got.tolist()}"
    got[0, 0] = -100.0
    if E[2, 0] != 6.0:
        return "lookup возвращает копию: изменение результата не должно менять E"
    if m.lookup(E, []).shape != (0, 3):
        return "пустой список id даёт массив формы (0, d)"


def t_cosine_basic():
    A = np.array([[3.0, 4.0], [0.0, 2.0]])
    n = m.l2_normalize(A)
    if not close(n, [[0.6, 0.8], [0.0, 1.0]]):
        return f"l2_normalize([[3, 4], [0, 2]]): ожидалось [[0.6, 0.8], [0, 1]], получили {n.tolist()}"
    if not close(m.l2_normalize(np.array([3.0, 4.0])), [0.6, 0.8]):
        return "l2_normalize должна работать и для 1-D вектора"
    B = np.array([[1.0, 0.0], [0.0, -5.0], [6.0, 8.0]])
    S = m.cosine_matrix(A, B)
    want = [[0.6, -0.8, 1.0], [0.0, -1.0, 0.8]]
    if S.shape != (2, 3) or not close(S, want):
        return f"cosine_matrix: ожидалось {want}, получили {S.tolist()}"


def t_top_k_basic():
    M = np.array([[1.0, 0.0], [0.0, 1.0], [1.0, 1.0], [-1.0, 0.0]])
    got = m.top_k(np.array([1.0, 0.2]), M, 2)
    if got.tolist() != [0, 2]:
        return f"top_k([1, 0.2], M, 2): ожидалось [0, 2], получили {got}"
    if got.dtype != np.int64:
        return f"top_k возвращает int64, получили {got.dtype}"
    if m.top_k(np.array([-3.0, 0.1]), M, 4).tolist() != [3, 1, 2, 0]:
        return "при k = n возвращаются все строки по убыванию косинуса"


def t_lookup_errors():
    for bad in ([4], [-1], [0, 1, -4], [1, 99]):
        try:
            m.lookup(E, bad)
        except ValueError:
            continue
        return f"lookup(E, {bad}) должна бросать ValueError (id вне 0..3; отрицательные индексы не должны считаться с конца)"


def t_normalize_zero():
    X = np.array([[0.0, 0.0, 0.0], [1e-15, 0.0, 0.0], [0.0, 2.0, 0.0]])
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        got = m.l2_normalize(X)
        z1 = m.l2_normalize(np.zeros(4))
    if not np.all(np.isfinite(got)) or not close(got, [[0, 0, 0], [0, 0, 0], [0, 1, 0]]):
        return f"строки с нормой меньше eps остаются нулевыми, остальные нормируются: получили {got.tolist()}"
    if not close(z1, np.zeros(4)):
        return "нулевой 1-D вектор остаётся нулевым"
    if not close(m.l2_normalize(np.array([[1e-15, 0.0]]), eps=1e-20), [[1.0, 0.0]]):
        return "порог eps задаётся параметром: при eps=1e-20 вектор (1e-15, 0) нормируется"
    X0 = np.array([[3.0, 4.0]])
    m.l2_normalize(X0)
    if X0[0, 0] != 3.0:
        return "l2_normalize не должна менять входной массив"


def t_cosine_edge():
    rng = np.random.default_rng(3)
    A = rng.normal(size=(4, 5))
    B = rng.normal(size=(6, 5))
    S = m.cosine_matrix(A, B)
    want = (A / np.linalg.norm(A, axis=1, keepdims=True)) @ (B / np.linalg.norm(B, axis=1, keepdims=True)).T
    if S.shape != (4, 6) or not close(S, want):
        return "cosine_matrix на случайных матрицах не совпала с эталоном"
    if not close(m.cosine_matrix(A * 1000.0, B * 0.001), S):
        return "косинус не должен зависеть от масштаба векторов"
    if not close(m.cosine_matrix(A, -B), -S):
        return "смена знака одной матрицы меняет знак сходства"
    Z = np.vstack([np.zeros((1, 5)), B[:2]])
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        SZ = m.cosine_matrix(Z, A)
        SZ2 = m.cosine_matrix(A, Z)
    if not close(SZ[0], np.zeros(4)) or not close(SZ2[:, 0], np.zeros(4)):
        return "сходство с нулевым вектором равно 0 (без nan и предупреждений)"
    v = np.array([[0.1, 0.2, 0.3]])
    s = m.cosine_matrix(v, v)
    if not (-1.0 <= s[0, 0] <= 1.0) or not close(s, [[1.0]], 1e-12):
        return "косинус вектора с самим собой равен 1 и не выходит за [-1, 1]"


def t_top_k_ties():
    M = np.array([[2.0, 0.0], [0.0, 1.0], [1.0, 0.0], [5.0, 0.0], [0.0, 3.0]])
    got = m.top_k(np.array([1.0, 0.0]), M, 3).tolist()
    if got != [0, 2, 3]:
        return f"строки 0, 2, 3 сонаправлены с запросом (косинус 1): при ничьей раньше идёт меньший индекс, ожидалось [0, 2, 3], получили {got}"
    got = m.top_k(np.array([0.0, 1.0]), M, 5).tolist()
    if got != [1, 4, 0, 2, 3]:
        return f"ожидалось [1, 4, 0, 2, 3], получили {got}"
    zero = m.top_k(np.zeros(2), M, 3)
    if zero.tolist() != [0, 1, 2]:
        return "для нулевого запроса все сходства равны 0, порядок - по индексу: [0, 1, 2]"


def t_top_k_bounds():
    M = np.array([[1.0, 0.0], [0.0, 1.0], [-1.0, 0.0]])
    q = np.array([1.0, 0.1])
    got = m.top_k(q, M, 10)
    if got.tolist() != [0, 1, 2] or got.dtype != np.int64:
        return f"k больше числа строк: вернуть все строки (int64) по убыванию, получили {got.tolist()} {got.dtype}"
    for k in (0, -3):
        got = m.top_k(q, M, k)
        if got.shape != (0,) or got.dtype != np.int64:
            return f"k = {k}: ожидался пустой массив int64, получили shape {got.shape}, dtype {got.dtype}"
    if m.top_k(q, np.zeros((0, 2)), 3).shape != (0,):
        return "матрица без строк даёт пустой результат"


def t_mean_pool():
    H = np.array(
        [
            [[1.0, 2.0], [3.0, 4.0], [100.0, 100.0]],
            [[5.0, 6.0], [7.0, 8.0], [9.0, 10.0]],
        ]
    )
    mask = np.array([[1, 1, 0], [1, 1, 1]])
    got = m.mean_pool(H, mask)
    if got.shape != (2, 2) or not close(got, [[2.0, 3.0], [7.0, 8.0]]):
        return f"mean_pool: ожидалось [[2, 3], [7, 8]], получили {got.tolist()}"
    got = m.mean_pool(H, mask.astype(bool))
    if not close(got, [[2.0, 3.0], [7.0, 8.0]]):
        return "маска типа bool должна работать так же, как маска из 0 и 1"
    got = m.mean_pool(H, np.array([[0, 1, 0], [1, 0, 1]]))
    if not close(got, [[3.0, 4.0], [7.0, 8.0]]):
        return f"маска может иметь единицы не только в начале: ожидалось [[3, 4], [7, 8]], получили {got.tolist()}"


def t_mean_pool_padding():
    rng = np.random.default_rng(5)
    H = rng.normal(size=(3, 4, 6))
    mask = np.array([[1, 1, 1, 1], [1, 1, 0, 0], [0, 0, 0, 0]])
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        base = m.mean_pool(H, mask)
        H2 = H.copy()
        H2[1, 2:] = 1e9
        H2[2] = np.nan
        H2[0, 0] = H[0, 0]
        changed = m.mean_pool(H2, mask)
    if not close(base[0], H[0].mean(axis=0)) or not close(base[1], H[1, :2].mean(axis=0)):
        return "среднее считается только по позициям с маской 1"
    if not close(base[2], np.zeros(6)):
        return "строка с полностью нулевой маской даёт нули (без nan и предупреждений)"
    if not np.all(np.isfinite(changed)) or not close(changed, base):
        return "значения на позициях с маской 0 (даже 1e9 и nan) не должны влиять на результат"


case("lookup-basic", t_lookup_basic)
case("cosine-basic", t_cosine_basic)
case("top-k-basic", t_top_k_basic)
case("lookup-errors", t_lookup_errors)
case("normalize-zero", t_normalize_zero)
case("cosine-edge", t_cosine_edge)
case("top-k-ties", t_top_k_ties)
case("top-k-bounds", t_top_k_bounds)
case("mean-pool", t_mean_pool)
case("mean-pool-padding", t_mean_pool_padding)
