import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import kmeans_task as m


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
import warnings

TRUE_CENTERS = np.array([[0.0, 0.0], [8.0, 0.0], [0.0, 8.0]])


def blobs(seed=0, per=40, std=0.5):
    rng = np.random.default_rng(seed)
    X = np.vstack([rng.normal(c, std, (per, 2)) for c in TRUE_CENTERS])
    truth = np.repeat(np.arange(3), per)
    return X, truth


def pure(labels, truth):
    """Каждому истинному кластеру соответствует ровно одна метка, и метки различны."""
    ids = [set(labels[truth == t].tolist()) for t in range(3)]
    return all(len(s) == 1 for s in ids) and len({next(iter(s)) for s in ids}) == 3


def t_assign_basic():
    X = np.array([[0.0, 0.0], [1.0, 0.0], [9.0, 9.0], [4.0, 0.0]])
    C = np.array([[0.0, 0.0], [10.0, 10.0]])
    got = m.assign(X, C)
    if got.dtype != np.int64 or got.shape != (4,) or got.tolist() != [0, 0, 1, 0]:
        return f"assign: ожидалось [0, 0, 1, 0] типа int64, получили {got!r}"
    tie = m.assign(np.array([[1.0, 0.0]]), np.array([[0.0, 0.0], [2.0, 0.0]]))
    if tie.tolist() != [0]:
        return "при равных расстояниях выбирается центр с меньшим индексом (np.argmin)"


def t_update_basic():
    X = np.array([[0.0, 0.0], [2.0, 0.0], [10.0, 10.0], [12.0, 14.0]])
    labels = np.array([0, 0, 1, 1])
    C = np.zeros((2, 2))
    got = m.update(X, labels, C)
    want = np.array([[1.0, 0.0], [11.0, 12.0]])
    if got.shape != (2, 2) or not np.allclose(got, want, rtol=0, atol=1e-12):
        return f"update: ожидалось {want.tolist()}, получили {got!r}"
    if not np.array_equal(C, np.zeros((2, 2))):
        return "update не должна изменять переданные центры на месте"


def t_inertia_basic():
    X = np.array([[0.0, 0.0], [2.0, 0.0], [10.0, 10.0]])
    C = np.array([[1.0, 0.0], [10.0, 9.0]])
    got = m.inertia(X, C, np.array([0, 0, 1]))
    if not isinstance(got, float) or not np.isclose(got, 1.0 + 1.0 + 1.0, rtol=1e-12, atol=0):
        return f"inertia: ожидалось 3.0 (float), получили {got!r}"


def t_kmeans_recovers_blobs():
    X, truth = blobs(0)
    centers, labels, inert, n_iter = m.kmeans(X, 3, seed=0)
    if centers.shape != (3, 2) or labels.shape != (120,) or labels.dtype != np.int64:
        return f"ожидались centers (3, 2) и labels (120,) int64, получили {centers.shape}, {labels.shape}, {labels.dtype}"
    if not pure(labels, truth):
        return "на трёх хорошо разделённых облаках кластеры должны совпасть с истинными (с точностью до перестановки меток)"
    d = np.linalg.norm(np.sort(centers, axis=0) - np.sort(TRUE_CENTERS, axis=0), axis=1).max()
    if d > 0.5:
        return f"центры должны быть близки к истинным (допуск 0.5), отклонение {d:.3f}"


def t_pp_init_d2():
    rng0 = np.random.default_rng(0)
    A = rng0.normal(0.0, 0.01, (50, 2))
    B = rng0.normal(100.0, 0.01, (50, 2))
    X = np.vstack([A, B])
    X0 = X.copy()
    for seed in range(20):
        c = m.kmeans_pp_init(X, 2, np.random.default_rng(seed))
        if c.shape != (2, 2) or np.linalg.norm(c[0] - c[1]) < 100:
            return f"k-means++ должен выбрать центры из разных групп (seed={seed}): вероятность пропорциональна D^2"
        if not all(any(np.array_equal(row, x) for x in X) for row in c):
            return "центры k-means++ должны быть строками X"
    if not np.array_equal(X, X0):
        return "инициализация не должна изменять X"
    a = m.kmeans_pp_init(X, 2, np.random.default_rng(7))
    b = m.kmeans_pp_init(X, 2, np.random.default_rng(7))
    if not np.array_equal(a, b):
        return "при одинаковом rng инициализация должна быть детерминированной"


def t_update_empty_cluster():
    X = np.array([[0.0, 0.0], [1.0, 0.0]])
    C = np.array([[0.0, 0.0], [1.0, 0.0], [100.0, 100.0]])
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        try:
            got = m.update(X, np.array([0, 1]), C)
        except RuntimeWarning as e:
            return f"update на пустом кластере порождает предупреждение ({e}): не усредняйте пустой срез"
    if not np.isfinite(got).all() or not np.allclose(got[2], [100.0, 100.0], rtol=0, atol=0):
        return f"пустой кластер должен сохранить прежний центр [100, 100], получили {got[2]!r}"


def t_kmeans_fixed_point():
    X, _ = blobs(3)
    centers, labels, inert, n_iter = m.kmeans(X, 3, seed=1, max_iter=200, tol=1e-9)
    if not np.array_equal(m.assign(X, centers), labels):
        return "итоговые labels должны быть назначением точек к итоговым центрам"
    again = m.update(X, labels, centers)
    if not np.allclose(again, centers, rtol=0, atol=1e-7):
        return "в точке сходимости центры - средние своих кластеров (допуск 1e-7)"
    if not np.isclose(inert, m.inertia(X, centers, labels), rtol=1e-12, atol=0):
        return "inertia должна быть согласована с итоговыми центрами и метками"
    if not (1 <= n_iter < 200):
        return f"на простых данных ожидалась сходимость быстрее max_iter, получили n_iter={n_iter}"


def t_kmeans_inertia_monotone():
    rng = np.random.default_rng(11)
    X = rng.normal(size=(150, 2))
    vals = [m.kmeans(X, 4, seed=2, max_iter=it, tol=0.0)[2] for it in range(1, 9)]
    if not all(b <= a + 1e-9 for a, b in zip(vals, vals[1:])):
        return f"инерция не должна расти с числом итераций Ллойда: {np.round(vals, 3).tolist()}"
    n_iter = m.kmeans(X, 4, seed=2, max_iter=1, tol=0.0)[3]
    if n_iter != 1:
        return f"при max_iter=1 должна быть выполнена ровно одна итерация, получили {n_iter}"


def t_kmeans_deterministic():
    X, truth = blobs(5)
    r1 = m.kmeans(X, 3, seed=4)
    r2 = m.kmeans(X, 3, seed=4)
    if not (np.array_equal(r1[0], r2[0]) and np.array_equal(r1[1], r2[1]) and r1[3] == r2[3]):
        return "при одинаковом seed результат должен совпадать полностью"
    for seed in range(6):
        if not pure(m.kmeans(X, 3, seed=seed)[1], truth):
            return f"k-means++ на разделённых облаках должен находить верное разбиение (seed={seed})"


def t_kmeans_degenerate():
    X = np.ones((10, 2))
    with warnings.catch_warnings():
        warnings.simplefilter("error")
        c, l, i, it = m.kmeans(X, 3, seed=0)
    if not (np.isfinite(c).all() and abs(i) < 1e-12):
        return "одинаковые точки: центры должны быть конечными, инерция - нулевой (проверьте деление на сумму d2 = 0)"
    c1, l1, i1, _ = m.kmeans(blobs(0)[0], 1, seed=0)
    if not np.allclose(c1[0], blobs(0)[0].mean(axis=0), rtol=0, atol=1e-9):
        return "при k = 1 центр должен быть средним всех точек"


case("assign-basic", t_assign_basic)
case("update-basic", t_update_basic)
case("inertia-basic", t_inertia_basic)
case("kmeans-recovers-blobs", t_kmeans_recovers_blobs)
case("pp-init-d2", t_pp_init_d2)
case("update-empty-cluster", t_update_empty_cluster)
case("kmeans-fixed-point", t_kmeans_fixed_point)
case("kmeans-inertia-monotone", t_kmeans_inertia_monotone)
case("kmeans-deterministic", t_kmeans_deterministic)
case("kmeans-degenerate", t_kmeans_degenerate)
