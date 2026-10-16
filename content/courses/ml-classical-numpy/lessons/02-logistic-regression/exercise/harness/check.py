import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import logreg_task as m


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


def blobs(seed=0, n=100, sep=1.5):
    rng = np.random.default_rng(seed)
    X = np.vstack([rng.normal(-sep, 1.0, (n, 2)), rng.normal(sep, 1.0, (n, 2))])
    y = np.concatenate([np.zeros(n), np.ones(n)])
    return X, y


def t_sigmoid_basic():
    z = np.array([-2.0, 0.0, 2.0])
    got = m.sigmoid(z)
    want = 1.0 / (1.0 + np.exp(-z))
    if got.shape != z.shape or not np.allclose(got, want, rtol=1e-12, atol=0):
        return f"sigmoid([-2, 0, 2]): ожидалось {want!r}, получили {got!r}"
    if not np.allclose(got + m.sigmoid(-z), 1.0, rtol=0, atol=1e-12):
        return "должно выполняться sigmoid(z) + sigmoid(-z) = 1"


def t_log_loss_values():
    z = np.array([0.0, 2.0, -1.0])
    y = np.array([1.0, 1.0, 0.0])
    p = 1.0 / (1.0 + np.exp(-z))
    want = float(-np.mean(y * np.log(p) + (1 - y) * np.log(1 - p)))
    got = m.log_loss(z, y)
    if not np.isclose(got, want, rtol=1e-12, atol=0):
        return f"log_loss: ожидалось {want!r}, получили {got!r}"
    if not isinstance(got, float):
        return f"log_loss должна вернуть float, получили {type(got).__name__}"


def t_gradient_small():
    Xb = np.array([[1.0, 0.0], [1.0, 2.0]])
    y = np.array([0.0, 1.0])
    w = np.zeros(2)
    got = m.gradient(Xb, y, w)
    want = np.array([0.0, -0.5])      # Xb^T (0.5 - y) / 2
    if got.shape != (2,) or not np.allclose(got, want, rtol=0, atol=1e-12):
        return f"gradient при w = 0: ожидалось {want!r}, получили {got!r}"


def t_fit_accuracy():
    X, y = blobs(0)
    w = m.fit(X, y, 0.5, 500)
    p = m.predict(X, w)
    acc = float(np.mean(p == y))
    if w.shape != (3,) or p.dtype != np.int64 or acc < 0.95:
        return f"после fit(lr=0.5, 500 эпох) ожидались w формы (3,), метки int64 и точность не ниже 0.95; получили {w.shape}, {p.dtype}, {acc:.3f}"


def t_sigmoid_extreme():
    z = np.array([-1000.0, -745.0, -30.0, 0.0, 30.0, 745.0, 1000.0])
    with np.errstate(over="raise", invalid="raise", divide="raise"):
        try:
            got = m.sigmoid(z)
        except FloatingPointError as e:
            return f"sigmoid переполняется на больших |z| ({e}); считайте exp только от неположительных аргументов"
    if not np.isfinite(got).all() or got[0] != 0.0 and got[0] > 1e-300 or got[-1] != 1.0 or got[3] != 0.5:
        return f"sigmoid на краях: ожидались 0 .. 0.5 .. 1 без nan, получили {got!r}"
    if not np.all(np.diff(got) >= 0):
        return "sigmoid должна быть монотонной"


def t_log_loss_extreme():
    with np.errstate(over="raise", invalid="raise", divide="raise"):
        try:
            a = m.log_loss(np.array([800.0, -800.0]), np.array([1.0, 0.0]))
            b = m.log_loss(np.array([800.0]), np.array([0.0]))
            c = m.log_loss(np.array([-800.0]), np.array([1.0]))
        except FloatingPointError as e:
            return f"log_loss даёт переполнение на больших логитах ({e}); используйте np.logaddexp(0, z) - y * z"
    if not (abs(a) < 1e-12 and abs(b - 800.0) < 1e-9 and abs(c - 800.0) < 1e-9):
        return f"ожидались 0, 800, 800 для уверенно верных/неверных логитов, получили {a!r}, {b!r}, {c!r}"


def t_gradient_finite_diff():
    X, y = blobs(1, n=20)
    Xb = np.hstack([np.ones((len(X), 1)), X])
    w = np.array([0.3, -0.5, 0.8])
    g = m.gradient(Xb, y, w)
    num = np.empty(3)
    for j in range(3):
        e = np.zeros(3)
        e[j] = 1e-6
        num[j] = (m.log_loss(Xb @ (w + e), y) - m.log_loss(Xb @ (w - e), y)) / 2e-6
    if g.shape != (3,) or not np.allclose(g, num, rtol=0, atol=1e-6):
        return f"градиент не совпал с конечными разностями потери: {g!r} против {num!r}"


def t_fit_loss_and_generalization():
    Xtr, ytr = blobs(2)
    Xte, yte = blobs(3)
    w = m.fit(Xtr, ytr, 0.5, 500)
    Xb = np.hstack([np.ones((len(Xtr), 1)), Xtr])
    w5 = m.fit(Xtr, ytr, 0.5, 5)
    if not m.log_loss(Xb @ w, ytr) < m.log_loss(Xb @ w5, ytr) < np.log(2):
        return "потеря должна убывать: loss(500 эпох) < loss(5 эпох) < ln 2 (потеря при w = 0)"
    acc = float(np.mean(m.predict(Xte, w) == yte))
    if acc < 0.94:
        return f"на новой выборке с тем же распределением ожидалась точность не ниже 0.94, получили {acc:.3f}"


def t_predict_threshold():
    X, y = blobs(0)
    w = m.fit(X, y, 0.5, 300)
    lo = m.predict(X, w, 0.1)
    mid = m.predict(X, w)
    hi = m.predict(X, w, 0.9)
    if not (lo.dtype == np.int64 and set(np.unique(mid)) <= {0, 1}):
        return "predict должна возвращать метки 0/1 типа int64"
    if not (lo.sum() >= mid.sum() >= hi.sum()) or lo.sum() == hi.sum():
        return "число положительных меток должно убывать с ростом порога (0.1 >= 0.5 >= 0.9) и порог должен влиять на результат"
    w1 = np.array([0.0, 1.0, 0.0])
    got = m.predict(np.array([[0.0, 5.0]]), w1, 0.5)
    if got[0] != 1:
        return "при вероятности ровно 0.5 и пороге 0.5 метка должна быть 1 (условие >=)"


def t_separable_finite():
    X = np.array([[-2.0], [-1.0], [1.0], [2.0]])
    y = np.array([0.0, 0.0, 1.0, 1.0])
    with np.errstate(over="raise", invalid="raise", divide="raise"):
        try:
            w = m.fit(X, y, 1.0, 3000)
        except FloatingPointError as e:
            return f"на линейно разделимых данных веса растут, но вычисления не должны переполняться ({e})"
    if not np.isfinite(w).all() or w[1] <= 0 or not np.array_equal(m.predict(X, w), y.astype(np.int64)):
        return f"на разделимых данных ожидался конечный w с положительным наклоном и верные метки, получили {w!r}"


case("sigmoid-basic", t_sigmoid_basic)
case("log-loss-values", t_log_loss_values)
case("gradient-small", t_gradient_small)
case("fit-accuracy", t_fit_accuracy)
case("sigmoid-extreme", t_sigmoid_extreme)
case("log-loss-extreme", t_log_loss_extreme)
case("gradient-finite-diff", t_gradient_finite_diff)
case("fit-loss-and-generalization", t_fit_loss_and_generalization)
case("predict-threshold", t_predict_threshold)
case("separable-finite", t_separable_finite)
