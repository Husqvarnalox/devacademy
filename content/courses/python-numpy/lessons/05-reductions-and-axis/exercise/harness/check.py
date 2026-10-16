import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import reduce_task as m


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


def t_col_means():
    a = np.array([[1, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 12]])
    out = m.col_means(a)
    if out.shape != (4,) or out.dtype != np.float64 or not np.allclose(out, [5.0, 6.0, 7.0, 8.0], rtol=0, atol=1e-12):
        return f"ожидалось [5. 6. 7. 8.] формы (4,), получили {out!r} {out.dtype}"


def t_row_argmax():
    a = np.array([[1, 9, 3], [7, 2, 5], [4, 4, 8], [0, 1, 0]])
    out = m.row_argmax(a)
    if out.shape != (4,) or not np.issubdtype(out.dtype, np.integer) or out.tolist() != [1, 0, 2, 1]:
        return f"ожидалось [1 0 2 1] формы (4,), получили {out!r}"


def t_softmax_basic():
    a = np.array([[0.0, 0.0, 0.0], [1.0, 2.0, 3.0]])
    out = m.stable_softmax(a)
    e = np.exp([1.0, 2.0, 3.0])
    want = np.vstack([np.full(3, 1 / 3), e / e.sum()])
    if out.shape != (2, 3) or out.dtype != np.float64 or not np.allclose(out, want, rtol=1e-12, atol=1e-12):
        return f"softmax неверен: {out!r}"


def t_variance_basic():
    a = np.array([2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0])
    v0 = m.variance(a)
    v1 = m.variance(a, ddof=1)
    if not (np.isclose(v0, 4.0, rtol=1e-12, atol=0) and np.isclose(v1, 32.0 / 7.0, rtol=1e-12, atol=0)):
        return f"ожидалось 4.0 (ddof=0) и {32 / 7:.6f} (ddof=1), получили {v0!r}, {v1!r}"


def t_axis_square_nonsymmetric():
    a = np.array([[1.0, 2.0, 30.0], [4.0, 50.0, 6.0], [70.0, 8.0, 9.0]])
    cm = m.col_means(a)
    if not np.allclose(cm, [25.0, 20.0, 15.0], rtol=0, atol=1e-12):
        return f"col_means: ожидалось [25. 20. 15.] (по столбцам, axis=0), получили {cm!r}; похоже, вы усредняете по строкам"
    ra = m.row_argmax(a)
    if ra.tolist() != [2, 1, 0]:
        return f"row_argmax: ожидалось [2 1 0] (индекс столбца максимума в каждой строке, axis=1), получили {ra.tolist()}"


def t_softmax_large_values():
    a = np.array([[1000.0, 1001.0, 999.0], [-1000.0, -1001.0, -999.0], [5000.0, 5000.0, 5000.0]])
    with np.errstate(all="raise"):
        try:
            out = m.stable_softmax(a)
        except FloatingPointError as exc:
            return f"переполнение или underflow в stable_softmax ({exc}): вычтите максимум строки до exp"
    if not np.isfinite(out).all():
        return "в результате есть nan/inf: вычтите максимум строки до exp"
    sums = out.sum(axis=1)
    if not np.allclose(sums, 1.0, rtol=0, atol=1e-12):
        return f"каждая строка должна суммироваться в 1, получили {sums}"
    e = np.exp(np.array([0.0, 1.0, -1.0]))
    if not np.allclose(out[0], e / e.sum(), rtol=1e-12, atol=1e-12) or not np.allclose(out[2], 1 / 3, rtol=0, atol=1e-12):
        return f"значения softmax неверны: {out!r}"


def t_variance_large_offset():
    x = 1e9 + np.arange(5, dtype=np.float64)
    got = m.variance(x)
    if not np.isclose(got, 2.0, rtol=1e-9, atol=0):
        return f"для 1e9 + [0..4] дисперсия равна 2.0, получили {got!r}: формула E[x^2] - E[x]^2 теряет точность, сначала вычтите среднее"
    rng = np.random.default_rng(5)
    y = 1e9 + rng.normal(size=500)
    want = float(((y - y.mean()) ** 2).sum() / 499)
    got1 = m.variance(y, ddof=1)
    if not np.isclose(got1, want, rtol=1e-9, atol=0):
        return f"seed-данные, ddof=1: ожидалось {want!r}, получили {got1!r}"


def t_variance_errors_and_ints():
    for a, dd in ((np.array([3.0]), 1), (np.array([]), 0), (np.array([1.0, 2.0]), 2)):
        try:
            m.variance(a, dd)
        except ValueError:
            continue
        return f"variance({a.tolist()}, ddof={dd}) должна бросать ValueError (n - ddof <= 0)"
    got = m.variance(np.array([1, 2, 3, 4]), ddof=1)
    if not isinstance(float(got), float) or not np.isclose(got, 5 / 3, rtol=1e-12, atol=0):
        return f"целый вход: ожидалось {5 / 3}, получили {got!r}"
    if not np.isclose(m.variance(np.array([5.0])), 0.0, rtol=0, atol=1e-15):
        return "дисперсия одного элемента при ddof=0 равна 0"


case("col-means", t_col_means)
case("row-argmax", t_row_argmax)
case("softmax-basic", t_softmax_basic)
case("variance-basic", t_variance_basic)
case("axis-square-nonsymmetric", t_axis_square_nonsymmetric)
case("softmax-large-values", t_softmax_large_values)
case("variance-large-offset", t_variance_large_offset)
case("variance-errors-and-ints", t_variance_errors_and_ints)
