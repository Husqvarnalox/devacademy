import copy
import json
import os
import random

NONCE = os.environ["DEVACADEMY_NONCE"]

import monitor as m


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


# ---------- эталонная реализация ----------

def ref_windowed(events, window):
    groups = {}
    for e in events:
        if e["label"] is None:
            continue
        groups.setdefault(e["ts"] // window, []).append(e["pred"] == e["label"])
    return [{"window": k, "n": len(v), "accuracy": round(sum(v) / len(v), 4)} for k, v in sorted(groups.items())]


def ref_rule(values, threshold, for_windows, direction="below"):
    out = []
    count = 0
    active = False
    for i, v in enumerate(values):
        if v is None:
            continue
        cond = (v < threshold) if direction == "below" else (v > threshold)
        if cond:
            count += 1
            if count == for_windows and not active:
                active = True
                out.append([i, "firing"])
            elif count > for_windows and not active:
                active = True
                out.append([i, "firing"])
        else:
            count = 0
            if active:
                active = False
                out.append([i, "resolved"])
    return out


def ref_decision(drift_rows, acc_rows, min_acc, min_samples=30):
    reasons = []
    for r in drift_rows:
        if r["status"] == "alert":
            reasons.append("drift:" + r["feature"])
    ok = [r for r in acc_rows if r["n"] >= min_samples]
    if not ok:
        reasons.append("quality:insufficient-data")
    elif ok[-1]["accuracy"] < min_acc:
        reasons.append("quality:%.4f<%.4f" % (ok[-1]["accuracy"], min_acc))
    retrain = any(x.startswith("drift:") or (x.startswith("quality:") and x != "quality:insufficient-data") for x in reasons)
    return {"retrain": retrain, "reasons": reasons}


def ev(ts, pred, label):
    return {"ts": ts, "pred": pred, "label": label}


def drow(feature, status):
    return {"feature": feature, "psi": 0.0, "ks": 0.0, "status": status}


def arow(window, n, accuracy):
    return {"window": window, "n": n, "accuracy": accuracy}


# ---------- публичные ----------

def t_windowed_basic():
    events = [ev(12, 1, 1), ev(3, 0, 0), ev(9, 1, 0), ev(10, 1, 1), ev(15, 0, None), ev(19, 0, 1), ev(35, 1, 1)]
    got = m.windowed_accuracy(events, 10)
    want = [{"window": 0, "n": 2, "accuracy": 0.5}, {"window": 1, "n": 3, "accuracy": 0.6667}, {"window": 3, "n": 1, "accuracy": 1.0}]
    if got != want:
        return (f"ожидалось {want}, получили {got}. Индекс окна ts // window (ts = 10 уже в окне 1), "
                "события без метки пропускаются, окна без размеченных событий в результат не входят, порядок по возрастанию окна")
    if m.windowed_accuracy([], 10) != []:
        return "пустой список событий - пустой результат"


def t_rule_basic():
    values = [0.9, 0.7, 0.6, 0.65, 0.9, 0.5, 0.9]
    got = m.evaluate_rule(values, 0.8, 2)
    want = [[2, "firing"], [4, "resolved"]]
    if [list(x) for x in got] != want:
        return f"ожидалось {want}, получили {got}: тревога включается на втором подряд плохом значении (индекс 2), выключается на первом хорошем (индекс 4); одиночное плохое значение в индексе 5 тревоги не даёт"
    got = m.evaluate_rule([0.5, 0.5, 0.5, 0.5], 0.8, 3)
    if [list(x) for x in got] != [[2, "firing"]]:
        return f"тревога включается один раз, пока продолжается серия: ожидалось [[2, 'firing']], получили {got}"


def t_rule_above_and_validation():
    got = m.evaluate_rule([1, 5, 6, 2], 4, 1, direction="above")
    if [list(x) for x in got] != [[1, "firing"], [3, "resolved"]]:
        return f"direction='above', порог 4, for_windows = 1: ожидалось [[1, 'firing'], [3, 'resolved']], получили {got}"
    if m.evaluate_rule([0.8, 0.8, 0.8], 0.8, 1) != [] or m.evaluate_rule([4, 4], 4, 1, "above") != []:
        return "значение, равное порогу, не нарушает условие ни при 'below', ни при 'above' (строгие сравнения)"
    if m.evaluate_rule([], 0.5, 2) != []:
        return "пустой ряд - пустой результат"
    try:
        m.evaluate_rule([1], 0.5, 1, direction="sideways")
    except ValueError:
        pass
    else:
        return "неизвестное direction: ожидалось ValueError"
    for bad in (0, -1):
        try:
            m.evaluate_rule([1], 0.5, bad)
        except ValueError:
            continue
        return f"for_windows = {bad}: ожидалось ValueError"


def t_retrain_basic():
    drift = [drow("a", "ok"), drow("b", "alert"), drow("c", "warn"), drow("d", "alert")]
    acc = [arow(0, 50, 0.91), arow(1, 40, 0.8)]
    got = m.retraining_decision(drift, acc, 0.85)
    want = {"retrain": True, "reasons": ["drift:b", "drift:d", "quality:0.8000<0.8500"]}
    if got != want:
        return f"ожидалось {want}, получили {got}: сначала drift: для строк alert в исходном порядке, затем причина по качеству"
    got = m.retraining_decision([drow("a", "ok"), drow("c", "warn")], [arow(0, 50, 0.91)], 0.85)
    if got != {"retrain": False, "reasons": []}:
        return f"дрейфа уровня alert нет, качество в норме: ожидалось {{'retrain': False, 'reasons': []}}, получили {got}"


# ---------- скрытые ----------

def t_windowed_edges():
    if m.windowed_accuracy([ev(1, 0, None), ev(2, 1, None)], 5) != []:
        return "все метки None: ожидался пустой список"
    events = [ev(t, t % 2, (t + (1 if t % 3 == 0 else 0)) % 2) for t in range(7)]
    got = m.windowed_accuracy(events, 1)
    want = ref_windowed(events, 1)
    if got != want or len(got) != 7:
        return f"window = 1: каждое ts в своём окне; ожидалось {want}, получили {got}"
    for bad in (0, -2):
        try:
            m.windowed_accuracy([ev(1, 0, 0)], bad)
        except ValueError:
            continue
        return f"window = {bad}: ожидалось ValueError"
    gap = [ev(0, 1, 1), ev(100, 1, 0), ev(101, 0, None), ev(200, 1, 1), ev(250, 1, 1)]
    got = m.windowed_accuracy(gap, 50)
    want = ref_windowed(gap, 50)
    if got != want or [r["window"] for r in got] != [0, 2, 4, 5]:
        return f"окна без размеченных событий пропускаются, индексы не сжимаются: ожидалось окна [0, 2, 4, 5], получили {[r['window'] for r in got]}"
    only_none = [ev(5, 1, 1), ev(7, 1, None), ev(8, 0, None)]
    got = m.windowed_accuracy(only_none, 10)
    if got != [{"window": 0, "n": 1, "accuracy": 1.0}]:
        return f"в окне считаются только размеченные события: ожидалось n = 1, получили {got}"
    rng = random.Random(7)
    big = [ev(rng.randrange(0, 1000), rng.randrange(2), rng.choice([0, 1, None])) for _ in range(500)]
    snapshot = copy.deepcopy(big)
    got = m.windowed_accuracy(big, 37)
    if got != ref_windowed(big, 37):
        return "случайные несортированные события: результат не совпал с эталоном"
    if big != snapshot:
        return "windowed_accuracy не должна изменять входной список"
    third = [ev(1, 1, 1), ev(2, 0, 1), ev(3, 1, 1)]
    if m.windowed_accuracy(third, 10) != [{"window": 0, "n": 3, "accuracy": 0.6667}]:
        return "2 верных из 3: accuracy = 0.6667 (round до 4 знаков)"
    exact = [ev(0, 1, 1), ev(10, 0, 0), ev(20, 1, 0)]
    got = m.windowed_accuracy(exact, 10)
    if [r["window"] for r in got] != [0, 1, 2]:
        return f"ts, кратные размеру окна, начинают новое окно: ожидалось [0, 1, 2], получили {[r['window'] for r in got]}"


def t_rule_edges():
    cases = [
        ([0.5, None, 0.5], 0.8, 2, "below", [[2, "firing"]]),
        ([0.5, 0.5, None, None, 0.9], 0.8, 2, "below", [[1, "firing"], [4, "resolved"]]),
        ([0.5, 0.9, 0.5, 0.9, 0.5], 0.8, 1, "below", [[0, "firing"], [1, "resolved"], [2, "firing"], [3, "resolved"], [4, "firing"]]),
        ([0.5, 0.5, 0.9, 0.5, 0.5, 0.9], 0.8, 3, "below", []),
        ([0.5, 0.5, 0.9, 0.5, 0.5, 0.5], 0.8, 3, "below", [[5, "firing"]]),
        ([0.5, 0.5, 0.5, 0.5, 0.5, 0.9, 0.5, 0.5, 0.5], 0.8, 3, "below", [[2, "firing"], [5, "resolved"], [8, "firing"]]),
        ([None, None, None], 0.8, 1, "below", []),
        ([0.5, None, 0.9, 0.5, 0.5], 0.8, 2, "below", [[4, "firing"]]),
        ([0.5, None, None, 0.5, 0.5], 0.8, 3, "below", [[4, "firing"]]),
        ([None, 0.9, None], 0.8, 1, "below", []),
        ([1, 9, 9, 9, 1, 9, 1], 5, 2, "above", [[2, "firing"], [4, "resolved"]]),
    ]
    for values, thr, k, direction, want in cases:
        got = [list(x) for x in m.evaluate_rule(values, thr, k, direction)]
        if got != want:
            return f"values = {values}, threshold = {thr}, for_windows = {k}, direction = {direction!r}: ожидалось {want}, получили {got}"
    values = [0.5, 0.9, None, 0.5]
    snapshot = list(values)
    m.evaluate_rule(values, 0.8, 1)
    if values != snapshot:
        return "evaluate_rule не должна изменять входной ряд"
    rng = random.Random(3)
    series = [rng.choice([0.6, 0.7, 0.85, 0.9, None]) for _ in range(300)]
    for k in (1, 2, 3, 5):
        if [list(x) for x in m.evaluate_rule(series, 0.8, k)] != ref_rule(series, 0.8, k):
            return f"случайный ряд из 300 значений, for_windows = {k}: результат не совпал с эталоном"


def t_retrain_edges():
    got = m.retraining_decision([], [arow(0, 50, 0.85)], 0.85)
    if got != {"retrain": False, "reasons": []}:
        return f"точность ровно равна минимуму - это не «ниже»: ожидалось {{'retrain': False, 'reasons': []}}, получили {got}"
    rows = [arow(0, 50, 0.6), arow(1, 10, 0.5)]
    got = m.retraining_decision([], rows, 0.85)
    if got != {"retrain": True, "reasons": ["quality:0.6000<0.8500"]}:
        return f"берётся последняя строка с n >= min_samples (не последняя вообще): ожидалось quality:0.6000<0.8500, получили {got}"
    rows = [arow(0, 50, 0.6), arow(1, 60, 0.9)]
    got = m.retraining_decision([], rows, 0.85)
    if got != {"retrain": False, "reasons": []}:
        return f"последняя подходящая строка в порядке: старое низкое значение не учитывается; получили {got}"
    got = m.retraining_decision([], [arow(0, 30, 0.5)], 0.85)
    if got != {"retrain": True, "reasons": ["quality:0.5000<0.8500"]}:
        return f"n = min_samples (30) уже достаточно (n >= min_samples), получили {got}"
    got = m.retraining_decision([], [arow(0, 29, 0.5)], 0.85)
    if got != {"retrain": False, "reasons": ["quality:insufficient-data"]}:
        return f"n = 29 < 30: ожидалось insufficient-data без переобучения, получили {got}"
    got = m.retraining_decision([], [], 0.85)
    if got != {"retrain": False, "reasons": ["quality:insufficient-data"]}:
        return f"нет строк точности: ожидалось insufficient-data, получили {got}"
    got = m.retraining_decision([drow("x", "alert")], [], 0.85)
    if got != {"retrain": True, "reasons": ["drift:x", "quality:insufficient-data"]}:
        return f"дрейф alert при нехватке данных: переобучать надо, причины по порядку; получили {got}"
    got = m.retraining_decision([drow("z", "alert"), drow("a", "alert")], [arow(0, 100, 0.123456)], 0.9)
    if got != {"retrain": True, "reasons": ["drift:z", "drift:a", "quality:0.1235<0.9000"]}:
        return f"порядок drift: как во входе, числа в причине с 4 знаками (:.4f); получили {got}"
    got = m.retraining_decision([], [arow(0, 5, 0.1), arow(1, 100, 0.95)], 0.9, min_samples=100)
    if got != {"retrain": False, "reasons": []}:
        return f"min_samples = 100: подходит только вторая строка; получили {got}"
    drift = [drow("a", "alert")]
    acc = [arow(0, 40, 0.5)]
    snapshot = (copy.deepcopy(drift), copy.deepcopy(acc))
    m.retraining_decision(drift, acc, 0.9)
    if (drift, acc) != snapshot:
        return "retraining_decision не должна изменять входные данные"


def t_end_to_end():
    rng = random.Random(2024)
    events = []
    for ts in range(600):
        label = rng.randrange(2)
        correct = rng.random() < (0.93 if ts < 300 else 0.72)
        pred = label if correct else 1 - label
        events.append(ev(ts, pred, None if (ts >= 540 or rng.random() < 0.05) else label))
    rng.shuffle(events)
    rows = m.windowed_accuracy(events, 60)
    want_rows = ref_windowed(events, 60)
    if rows != want_rows:
        return f"windowed_accuracy на 600 событиях с запоздавшими метками не совпала с эталоном (последнее окно почти без меток): ожидалось {want_rows}, получили {rows}"
    if len(rows) != 9 or rows[-1]["window"] != 8:
        return f"окно 9 состоит только из событий без метки и в результат не входит: ожидалось 9 окон, получили {len(rows)}"
    series = [None] * 10
    for r in rows:
        series[r["window"]] = r["accuracy"]
    got = [list(x) for x in m.evaluate_rule(series, 0.85, 2)]
    want = ref_rule(series, 0.85, 2)
    if got != want:
        return f"правило по ряду точности {series}: ожидалось {want}, получили {got}"
    if not want or want[0][1] != "firing":
        return "внутренняя ошибка проверки: сценарий должен давать тревогу"
    drift = [{"feature": "age", "psi": 0.31, "ks": 0.2, "status": "alert"}, {"feature": "income", "psi": 0.02, "ks": 0.04, "status": "ok"}]
    decision = m.retraining_decision(drift, rows, 0.85, min_samples=30)
    if decision != ref_decision(drift, rows, 0.85, 30):
        return f"решение о переобучении: ожидалось {ref_decision(drift, rows, 0.85, 30)}, получили {decision}"
    if not decision["retrain"]:
        return "в сценарии точность упала ниже 0.85 и есть drift: ожидался retrain = True"


case("windowed-basic", t_windowed_basic)
case("rule-basic", t_rule_basic)
case("rule-above-and-validation", t_rule_above_and_validation)
case("retrain-basic", t_retrain_basic)
case("windowed-edges", t_windowed_edges)
case("rule-edges", t_rule_edges)
case("retrain-edges", t_retrain_edges)
case("end-to-end", t_end_to_end)
