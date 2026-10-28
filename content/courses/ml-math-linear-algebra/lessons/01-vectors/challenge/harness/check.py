import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import centroids_task as m


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


def loop_centroids(X, labels, old):
    out = [list(map(float, row)) for row in old]
    for j in range(len(old)):
        members = [X[i] for i in range(len(X)) if labels[i] == j]
        if members:
            out[j] = [sum(float(p[c]) for p in members) / len(members) for c in range(len(X[0]))]
    return np.array(out)


def t_centroids_basic():
    X = np.array([[1.0, 2.0], [3.0, 2.0], [2.0, 5.0], [10.0, 10.0], [12.0, 8.0]])
    labels = np.array([0, 0, 0, 1, 1])
    old = np.zeros((2, 2))
    got = m.update_centroids(X, labels, old)
    want = np.array([[2.0, 3.0], [11.0, 9.0]])
    if not isinstance(got, np.ndarray) or got.shape != (2, 2):
        return f"ожидался массив формы (2, 2), получили {type(got).__name__} {getattr(got, 'shape', None)}"
    if not np.allclose(got, want, rtol=0, atol=1e-12):
        return f"центры должны быть {want.tolist()}, получили {got.tolist()}"


def t_momentum_basic():
    w = np.array([0.5, -1.0])
    v = np.array([1.0, 0.0])
    g = np.array([2.0, -4.0])
    new_w, new_v = m.momentum_step(w, v, g, 0.1, 0.9)
    if not np.allclose(new_v, [2.9, -4.0], rtol=0, atol=1e-12):
        return f"new_velocity = 0.9 * [1, 0] + [2, -4] = [2.9, -4], получили {np.asarray(new_v).tolist()}"
    if not np.allclose(new_w, [0.21, -0.6], rtol=0, atol=1e-12):
        return f"new_w = w - 0.1 * new_velocity = [0.21, -0.6], получили {np.asarray(new_w).tolist()}"


def t_centroids_random_vs_loop():
    rng = np.random.default_rng(7)
    X = rng.normal(size=(60, 4))
    labels = rng.integers(0, 5, size=60)
    old = rng.normal(size=(5, 4))
    got = m.update_centroids(X, labels, old)
    want = loop_centroids(X.tolist(), labels.tolist(), old.tolist())
    if not np.allclose(got, want, rtol=1e-12, atol=1e-12):
        return "центры не совпали с покомпонентным средним, посчитанным циклом"


def t_empty_cluster():
    X = np.array([[1.0, 1.0], [3.0, 3.0]])
    labels = np.array([0, 0])
    old = np.array([[0.0, 0.0], [7.0, -7.0], [5.0, 5.0]])
    got = m.update_centroids(X, labels, old)
    if not np.all(np.isfinite(got)):
        return "пустой кластер не должен давать nan: оставьте старый центр"
    if not np.allclose(got, [[2.0, 2.0], [7.0, -7.0], [5.0, 5.0]], rtol=0, atol=1e-12):
        return f"для пустых кластеров 1 и 2 центр не меняется, получили {got.tolist()}"


def t_int_input_and_no_mutation():
    X = np.array([[1, 2], [2, 3]])
    labels = np.array([0, 0])
    old = np.array([[0, 0], [9, 9]])
    old_copy = old.copy()
    got = m.update_centroids(X, labels, old)
    if not np.array_equal(old, old_copy):
        return "update_centroids не должна изменять old_centroids на месте"
    if got.dtype.kind != "f" or not np.allclose(got, [[1.5, 2.5], [9.0, 9.0]]):
        return f"центры должны быть float: [[1.5, 2.5], [9, 9]], получили {got.tolist()} dtype={got.dtype}"
    w = np.array([1.0, 1.0])
    v = np.array([0.5, 0.5])
    g = np.array([1.0, -1.0])
    w0, v0 = w.copy(), v.copy()
    m.momentum_step(w, v, g, 0.5, 0.5)
    if not (np.array_equal(w, w0) and np.array_equal(v, v0)):
        return "momentum_step не должна изменять w и velocity на месте (используйте w - ..., а не w -= ...)"


def t_momentum_zero_beta():
    rng = np.random.default_rng(3)
    w = rng.normal(size=5)
    v = rng.normal(size=5)
    g = rng.normal(size=5)
    new_w, new_v = m.momentum_step(w, v, g, 0.01, 0.0)
    if not (np.allclose(new_v, g) and np.allclose(new_w, w - 0.01 * g)):
        return "при beta = 0 шаг с моментом совпадает с обычным шагом w - lr * grad"


case("centroids-basic", t_centroids_basic)
case("momentum-basic", t_momentum_basic)
case("centroids-random-vs-loop", t_centroids_random_vs_loop)
case("empty-cluster", t_empty_cluster)
case("int-input-no-mutation", t_int_input_and_no_mutation)
case("momentum-zero-beta", t_momentum_zero_beta)
