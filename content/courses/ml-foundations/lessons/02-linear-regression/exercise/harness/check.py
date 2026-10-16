import json
import math
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import regression_task as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def closed_form(xs, ys):
    """Эталон: аналитическое решение метода наименьших квадратов."""
    n = len(xs)
    mx = sum(xs) / n
    my = sum(ys) / n
    sxx = sum((x - mx) ** 2 for x in xs)
    sxy = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    w = sxy / sxx
    return w, my - w * mx


def close(a, b, tol):
    return isinstance(a, (int, float)) and math.isfinite(a) and abs(a - b) <= tol


def safe_mse(w, b, xs, ys):
    """mse, но переполнение float при расходимости считаем бесконечной потерей."""
    try:
        return m.mse(w, b, xs, ys)
    except OverflowError:
        return math.inf


def pair(res):
    w, b = res
    return float(w), float(b)


LINE_X = [0, 1, 2, 3, 4]
LINE_Y = [1, 3, 5, 7, 9]

NOISY_X = [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0]
NOISY_Y = [2.3, 4.1, 5.8, 8.4, 9.7, 12.2, 13.9, 16.1]

BIG_X = [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000]
BIG_Y = [80.5, 129.0, 182.0, 230.5, 281.0, 330.0, 379.5, 432.0, 478.5, 530.0]


def t_mse():
    got = m.mse(2.0, 1.0, LINE_X, LINE_Y)
    if not close(got, 0.0, 1e-12):
        return f"mse(2, 1) на точных данных должно быть 0, получили {got!r}"
    got = m.mse(0.0, 0.0, [1.0, 2.0, 3.0], [1.0, 2.0, 3.0])
    if not close(got, (1 + 4 + 9) / 3, 1e-12):
        return f"mse(0, 0, [1,2,3], [1,2,3]) должно быть {14 / 3:.6f}, получили {got!r}"
    got = m.mse(1.0, 1.0, [0.0, 2.0], [0.0, 0.0])
    if not close(got, (1 + 9) / 2, 1e-12):
        return f"mse(1, 1, [0,2], [0,0]) должно быть 5.0, получили {got!r}"


def t_fit_exact_line():
    w, b = pair(m.fit(LINE_X, LINE_Y, 0.05, 2000))
    if not (close(w, 2.0, 1e-3) and close(b, 1.0, 1e-3)):
        return f"на точной прямой y = 2x + 1 ожидалось w = 2, b = 1 (допуск 1e-3), получили w = {w:.5f}, b = {b:.5f}"


def t_fit_noisy_closed_form():
    wc, bc = closed_form(NOISY_X, NOISY_Y)
    w, b = pair(m.fit(NOISY_X, NOISY_Y, 0.01, 20000))
    if not (close(w, wc, 1e-3) and close(b, bc, 1e-3)):
        return f"эталон МНК: w = {wc:.5f}, b = {bc:.5f}; получили w = {w:.5f}, b = {b:.5f} (допуск 1e-3)"


def t_zero_epochs_and_first_step():
    w, b = pair(m.fit(LINE_X, LINE_Y, 0.05, 0))
    if (w, b) != (0.0, 0.0):
        return f"при epochs=0 должны вернуться начальные значения (0, 0), получили ({w}, {b})"
    # Один шаг вручную: dw = 2/n * sum(err*x), db = 2/n * sum(err), err = -y при w=b=0
    n = len(LINE_X)
    dw = 2 * sum(-y * x for x, y in zip(LINE_X, LINE_Y)) / n
    db = 2 * sum(-y for y in LINE_Y) / n
    lr = 0.01
    w, b = pair(m.fit(LINE_X, LINE_Y, lr, 1))
    if not (close(w, -lr * dw, 1e-9) and close(b, -lr * db, 1e-9)):
        return f"после одного шага ожидалось w = {-lr * dw:.6f}, b = {-lr * db:.6f}; получили w = {w:.6f}, b = {b:.6f}"


def t_loss_decreases():
    prev = m.mse(0.0, 0.0, NOISY_X, NOISY_Y)
    for epochs in (1, 2, 5, 20, 100, 500):
        w, b = pair(m.fit(NOISY_X, NOISY_Y, 0.005, epochs))
        cur = m.mse(w, b, NOISY_X, NOISY_Y)
        if cur > prev + 1e-12:
            return f"при малом lr потери не должны расти: после {epochs} эпох MSE = {cur:.5f} > {prev:.5f}"
        prev = cur


def t_large_lr_diverges():
    start = m.mse(0.0, 0.0, LINE_X, LINE_Y)
    try:
        w, b = pair(m.fit(LINE_X, LINE_Y, 0.2, 40))
        end = safe_mse(w, b, LINE_X, LINE_Y)
    except OverflowError:
        end = math.inf
    if not (end > 100 * start or not math.isfinite(end)):
        return "при слишком большом шаге (lr = 0.2 на этих данных) градиентный спуск должен расходиться; похоже, это не градиентный спуск, а готовая формула"


def t_fit_scaled_big_features():
    wc, bc = closed_form(BIG_X, BIG_Y)
    w, b = pair(m.fit_scaled(BIG_X, BIG_Y, 0.1, 1000))
    if not (close(w, wc, 1e-5) and close(b, bc, 1e-4)):
        return f"эталон МНК: w = {wc:.6f}, b = {bc:.5f}; получили w = {w:.6f}, b = {b:.5f}"


def t_unscaled_fails_scaled_works():
    try:
        w, b = pair(m.fit(BIG_X, BIG_Y, 0.1, 1000))
        loss_plain = safe_mse(w, b, BIG_X, BIG_Y)
    except OverflowError:
        loss_plain = math.inf
    ws, bs = pair(m.fit_scaled(BIG_X, BIG_Y, 0.1, 1000))
    loss_scaled = m.mse(ws, bs, BIG_X, BIG_Y)
    if math.isfinite(loss_plain) and loss_plain < 1e3:
        return "fit без масштабирования должен расходиться на признаках порядка 1000 при lr = 0.1; проверьте, что fit - обычный градиентный спуск"
    if not (loss_scaled < 10):
        return f"fit_scaled должен сойтись (MSE < 10), получили {loss_scaled!r}"


def t_fit_scaled_constant_x():
    w, b = pair(m.fit_scaled([3, 3, 3, 3], [1.0, 2.0, 3.0, 6.0], 0.1, 100))
    if not (close(w, 0.0, 1e-9) and close(b, 3.0, 1e-9)):
        return f"при постоянном x стандартное отклонение равно 0: ожидалось w = 0, b = mean(y) = 3, получили w = {w}, b = {b}"


case("mse-basic", t_mse)
case("fit-exact-line", t_fit_exact_line)
case("fit-noisy-closed-form", t_fit_noisy_closed_form)
case("fit-first-step", t_zero_epochs_and_first_step)
case("loss-decreases", t_loss_decreases)
case("large-lr-diverges", t_large_lr_diverges)
case("fit-scaled-big-features", t_fit_scaled_big_features)
case("unscaled-diverges", t_unscaled_fails_scaled_works)
case("fit-scaled-constant-x", t_fit_scaled_constant_x)
