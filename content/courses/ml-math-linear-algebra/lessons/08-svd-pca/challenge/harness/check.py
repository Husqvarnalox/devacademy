import json
import os

import numpy as np

NONCE = os.environ["DEVACADEMY_NONCE"]

import lowrank_task as m


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


def low_rank_matrix(seed, m_, n_, r):
    rng = np.random.default_rng(seed)
    return rng.normal(size=(m_, r)) @ rng.normal(size=(r, n_))


def t_error_formula():
    rng = np.random.default_rng(3)
    A = rng.normal(size=(8, 5))
    s = np.linalg.svd(A, compute_uv=False)
    for k in range(0, 6):
        got = m.approximation_error(A, k)
        want = float(np.sqrt(np.sum(s[k:] ** 2)))
        if abs(got - want) > 1e-9:
            return f"при k = {k} ошибка Фробениуса равна sqrt(sum(s[k:] ** 2)) = {want}, получили {got}"
    Ak = m.low_rank_approx(A, 2)
    if Ak.shape != (8, 5) or np.linalg.matrix_rank(Ak) != 2:
        return "приближение должно иметь исходную форму (8, 5) и ранг 2"


def t_exact_low_rank():
    A = low_rank_matrix(5, 12, 7, 3)
    if not np.allclose(m.low_rank_approx(A, 3), A, atol=1e-9):
        return "матрица ранга 3 восстанавливается точно при k = 3"
    if m.approximation_error(A, 3) > 1e-9:
        return "ошибка при k = rank должна быть близка к нулю"
    if m.approximation_error(A, 2) < 1e-3:
        return "при k < rank ошибка заметно положительна"
    if not np.allclose(m.low_rank_approx(A, 7), A, atol=1e-9):
        return "при k = min(m, n) матрица восстанавливается точно"


def t_choose_rank():
    A = np.diag([3.0, 2.0, 1.0])  # s^2 = 9, 4, 1; доли 9/14, 13/14, 1
    cases = [(0.5, 1), (0.64, 1), (0.65, 2), (0.9, 2), (0.92, 2), (0.95, 3), (1.0, 3)]
    for energy, want in cases:
        got = m.choose_rank(A, energy)
        if got != want:
            return f"для diag(3, 2, 1) и energy = {energy} ожидалось k = {want}, получили {got}"
    B = low_rank_matrix(7, 10, 6, 2)
    if m.choose_rank(B, 1.0) != 2:
        return "при energy = 1.0 нужно вернуть ранг матрицы (2), а не min(m, n)"


def t_monotone_and_optimal():
    rng = np.random.default_rng(11)
    A = rng.normal(size=(9, 6))
    errs = [m.approximation_error(A, k) for k in range(7)]
    if any(b > a + 1e-12 for a, b in zip(errs, errs[1:])):
        return f"ошибка не должна расти с увеличением k: {errs}"
    if abs(errs[0] - np.linalg.norm(A)) > 1e-9:
        return "при k = 0 ошибка равна норме Фробениуса самой матрицы"
    if errs[6] > 1e-9:
        return "при k = min(m, n) ошибка равна нулю"
    U, s, Vt = np.linalg.svd(A, full_matrices=False)
    worse = (U[:, 2:4] * s[2:4]) @ Vt[2:4]  # ранг 2, но из неглавных сингулярных троек
    if np.linalg.norm(A - m.low_rank_approx(A, 2)) >= np.linalg.norm(A - worse):
        return "приближение из главных троек должно быть лучше, чем из неглавных"


def t_edges_and_shapes():
    A = np.array([[1, 2, 3], [4, 5, 6]])
    A0 = A.copy()
    if not (raises_value_error(m.low_rank_approx, A, -1) and raises_value_error(m.low_rank_approx, A, 3)):
        return "k < 0 и k > min(m, n) = 2 должны давать ValueError"
    z = m.low_rank_approx(A, 0)
    if z.shape != (2, 3) or np.any(z != 0):
        return "k = 0 даёт нулевую матрицу формы (2, 3)"
    full = m.low_rank_approx(A, 2)
    if full.shape != (2, 3) or full.dtype.kind != "f" or not np.allclose(full, A, atol=1e-9):
        return "для целочисленной A при k = 2 ожидалась float-копия исходной матрицы"
    if not np.array_equal(A, A0):
        return "входной массив не должен изменяться"
    wide = low_rank_matrix(2, 3, 10, 2)
    if m.low_rank_approx(wide, 1).shape != (3, 10):
        return "форма результата для широкой матрицы (3, 10) сохраняется"


def t_choose_rank_edges():
    A = np.diag([3.0, 2.0, 1.0])
    if m.choose_rank(A, 1e-9) != 1:
        return "очень малая доля энергии достигается уже при k = 1"
    if m.choose_rank(np.zeros((3, 4)), 0.9) != 0:
        return "для нулевой матрицы нужно вернуть 0"
    for bad in (0.0, -0.1, 1.5):
        if not raises_value_error(m.choose_rank, A, bad):
            return f"energy = {bad} вне (0, 1]: ожидался ValueError"
    if m.choose_rank(np.eye(4), 1.0) != 4:
        return "у единичной матрицы 4 x 4 нужно 4 компоненты для полной энергии"


def t_storage_ratio():
    if abs(m.storage_ratio(100, 50, 5) - 0.151) > 1e-12:
        return f"5 * (100 + 50 + 1) / (100 * 50) = 0.151, получили {m.storage_ratio(100, 50, 5)!r}"
    if m.storage_ratio(10, 10, 0) != 0:
        return "при k = 0 хранить нечего"
    if abs(m.storage_ratio(4, 4, 2) - 18 / 16) > 1e-12:
        return "для 4 x 4 при k = 2 тройки занимают больше места, чем сама матрица: 2 * 9 / 16"


case("error-formula", t_error_formula)
case("exact-low-rank", t_exact_low_rank)
case("choose-rank", t_choose_rank)
case("monotone-optimal", t_monotone_and_optimal)
case("edges-shapes", t_edges_and_shapes)
case("choose-rank-edges", t_choose_rank_edges)
case("storage-ratio", t_storage_ratio)
