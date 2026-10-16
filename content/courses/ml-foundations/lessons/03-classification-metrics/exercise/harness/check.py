import json
import math
import os

NONCE = os.environ["DEVACADEMY_NONCE"]

import metrics_task as m

TOL = 1e-9


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


def num(name, got, want):
    if not isinstance(got, (int, float)) or isinstance(got, bool) or not math.isfinite(got) or abs(got - want) > TOL:
        return f"{name} вернула {got!r}, ожидалось {want:.9f} (допуск {TOL})"
    return None


# Кейс из лекции: 10 объектов
Y_TRUE = [1, 1, 1, 1, 0, 0, 0, 0, 0, 0]
Y_PRED = [1, 1, 1, 0, 1, 1, 0, 0, 0, 0]
# tp=3 fp=2 fn=1 tn=4

# Дисбаланс: 5 положительных из 100, модель всегда говорит 0
IMB_TRUE = [1] * 5 + [0] * 95
IMB_PRED = [0] * 100

# Модель находит все положительные, но много ложных тревог
ALARM_TRUE = [1] * 4 + [0] * 16
ALARM_PRED = [1] * 4 + [1] * 6 + [0] * 10


def t_confusion():
    got = tuple(m.confusion_matrix(Y_TRUE, Y_PRED))
    if got != (3, 2, 1, 4):
        return f"confusion_matrix на примере вернула {got}, ожидалось (tp, fp, fn, tn) = (3, 2, 1, 4)"
    got = tuple(m.confusion_matrix([0, 1], [1, 0]))
    if got != (0, 1, 1, 0):
        return f"confusion_matrix([0, 1], [1, 0]) вернула {got}, ожидалось (0, 1, 1, 0)"


def t_accuracy():
    return num("accuracy", m.accuracy(Y_TRUE, Y_PRED), 0.7) or num("accuracy (все верно)", m.accuracy([1, 0, 1], [1, 0, 1]), 1.0)


def t_precision_recall():
    return (
        num("precision", m.precision(Y_TRUE, Y_PRED), 3 / 5)
        or num("recall", m.recall(Y_TRUE, Y_PRED), 3 / 4)
    )


def t_f1():
    p, r = 3 / 5, 3 / 4
    return num("f1", m.f1(Y_TRUE, Y_PRED), 2 * p * r / (p + r))


def t_imbalanced_accuracy_lies():
    return (
        num("accuracy на дисбалансе", m.accuracy(IMB_TRUE, IMB_PRED), 0.95)
        or num("recall на дисбалансе", m.recall(IMB_TRUE, IMB_PRED), 0.0)
        or num("precision при отсутствии предсказанных положительных", m.precision(IMB_TRUE, IMB_PRED), 0.0)
        or num("f1 на дисбалансе", m.f1(IMB_TRUE, IMB_PRED), 0.0)
    )


def t_high_recall_low_precision():
    p = 4 / 10
    return (
        num("recall", m.recall(ALARM_TRUE, ALARM_PRED), 1.0)
        or num("precision", m.precision(ALARM_TRUE, ALARM_PRED), p)
        or num("f1", m.f1(ALARM_TRUE, ALARM_PRED), 2 * p * 1.0 / (p + 1.0))
        or num("accuracy", m.accuracy(ALARM_TRUE, ALARM_PRED), 14 / 20)
    )


def t_no_positives_in_truth():
    yt = [0, 0, 0, 0]
    yp = [1, 0, 0, 1]
    return (
        num("recall без положительных в разметке", m.recall(yt, yp), 0.0)
        or num("precision", m.precision(yt, yp), 0.0)
        or num("f1", m.f1(yt, yp), 0.0)
        or num("accuracy", m.accuracy(yt, yp), 0.5)
    )


def t_empty_input():
    return (
        num("accuracy([])", m.accuracy([], []), 0.0)
        or num("f1([])", m.f1([], []), 0.0)
        or ("" if tuple(m.confusion_matrix([], [])) == (0, 0, 0, 0) else "confusion_matrix([], []) должна вернуть (0, 0, 0, 0)") or None
    )


def t_f1_is_harmonic():
    yt = [1, 1, 1, 1, 1, 1, 0, 0, 0, 0]
    yp = [1, 1, 1, 1, 1, 1, 1, 1, 1, 0]
    p, r = m.precision(yt, yp), m.recall(yt, yp)
    f = m.f1(yt, yp)
    if abs(f - 2 * p * r / (p + r)) > TOL:
        return f"f1 = {f!r}, а 2*p*r/(p+r) = {2 * p * r / (p + r)!r}"
    if f >= (p + r) / 2:
        return "среднее гармоническое должно быть строго меньше арифметического при p != r; проверьте формулу"


case("confusion", t_confusion)
case("accuracy", t_accuracy)
case("precision-recall", t_precision_recall)
case("f1", t_f1)
case("imbalanced-accuracy-lies", t_imbalanced_accuracy_lies)
case("high-recall-low-precision", t_high_recall_low_precision)
case("no-positives-in-truth", t_no_positives_in_truth)
case("empty-input", t_empty_input)
case("f1-is-harmonic", t_f1_is_harmonic)
