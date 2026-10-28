import json
import math
import os
import random

NONCE = os.environ["DEVACADEMY_NONCE"]

import drift as m


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

def ref_edges(ref, bins):
    s = sorted(ref)
    return [s[i * len(s) // bins] for i in range(1, bins)]


def ref_fracs(values, edges):
    if not values:
        return [0.0] * (len(edges) + 1)
    counts = [0] * (len(edges) + 1)
    for x in values:
        k = 0
        while k < len(edges) and edges[k] <= x:
            k += 1
        counts[k] += 1
    return [c / len(values) for c in counts]


def ref_psi(ref, cur, bins=10, eps=1e-4):
    edges = ref_edges(ref, bins)
    e = [max(x, eps) for x in ref_fracs(ref, edges)]
    a = [max(x, eps) for x in ref_fracs(cur, edges)]
    return sum((ai - ei) * math.log(ai / ei) for ai, ei in zip(a, e))


def ref_ks(a, b):
    sa, sb = sorted(a), sorted(b)
    i = j = 0
    best = 0.0
    while i < len(sa) or j < len(sb):
        x = min(sa[i] if i < len(sa) else math.inf, sb[j] if j < len(sb) else math.inf)
        while i < len(sa) and sa[i] == x:
            i += 1
        while j < len(sb) and sb[j] == x:
            j += 1
        best = max(best, abs(i / len(sa) - j / len(sb)))
    return best


def ref_report(ref, cur, bins=10, warn=0.1, alert=0.25):
    out = []
    for f in sorted(ref):
        if f not in cur or len(cur[f]) == 0:
            out.append({"feature": f, "psi": None, "ks": None, "status": "missing"})
            continue
        p = ref_psi(ref[f], cur[f], bins)
        k = ref_ks(ref[f], cur[f])
        out.append({"feature": f, "psi": round(p, 4), "ks": round(k, 4),
                    "status": "ok" if p < warn else "warn" if p < alert else "alert"})
    return out


def gauss(seed, n, mu=0.0, sd=1.0):
    rng = random.Random(seed)
    return [rng.gauss(mu, sd) for _ in range(n)]


def near(a, b, tol=1e-9):
    return a is not None and abs(a - b) <= tol


REF = gauss(1, 2000)
SAME = gauss(2, 2000)
SHIFT = gauss(4, 2000, 2.0)
SCALED = gauss(5, 2000, 0.0, 1.5)


# ---------- публичные ----------

def t_edges_and_fractions():
    got = m.quantile_edges(list(range(100)), 4)
    if list(got) != [25, 50, 75]:
        return f"quantile_edges(range(100), 4): ожидалось [25, 50, 75], получили {got!r}"
    got = m.quantile_edges(list(range(10)), 5)
    if list(got) != [2, 4, 6, 8]:
        return f"quantile_edges(range(10), 5): ожидалось [2, 4, 6, 8] (s[i * n // bins]), получили {got!r}"
    got = m.quantile_edges([5, 1, 4, 2, 3, 0], 3)
    if list(got) != [2, 4]:
        return f"выборка сначала сортируется: для [5, 1, 4, 2, 3, 0] и bins = 3 ожидалось [2, 4], получили {got!r}"
    got = m.bin_fractions([1, 2, 3, 4, 5, 6], [2, 4])
    want = [1 / 6, 2 / 6, 3 / 6]
    if len(got) != 3 or not all(near(g, w, 1e-12) for g, w in zip(got, want)):
        return f"bin_fractions([1..6], [2, 4]): значение, равное границе, уходит в правый бин; ожидалось {want}, получили {got!r}"
    got = m.bin_fractions([-5, 100], [0, 10])
    if len(got) != 3 or not all(near(g, w, 1e-12) for g, w in zip(got, [0.5, 0.0, 0.5])):
        return f"значения вне диапазона границ попадают в крайние бины: ожидалось [0.5, 0.0, 0.5], получили {got!r}"
    got = m.bin_fractions([], [1, 2])
    if list(got) != [0.0, 0.0, 0.0]:
        return f"пустой список значений: ожидалось [0.0, 0.0, 0.0], получили {got!r}"


def t_psi_basic():
    got = m.psi(REF, REF)
    if not near(got, 0.0, 1e-12):
        return f"psi выборки с самой собой должен быть 0, получили {got!r}"
    for name, cur in (("похожая выборка", SAME), ("сдвиг среднего на 2", SHIFT), ("масштаб 1.5", SCALED)):
        got, want = m.psi(REF, cur), ref_psi(REF, cur)
        if not near(got, want):
            return f"psi ({name}): ожидалось {want:.9f}, получили {got!r}"
    got, want = m.psi(REF, SHIFT, bins=5), ref_psi(REF, SHIFT, 5)
    if not near(got, want):
        return f"psi при bins = 5: ожидалось {want:.9f}, получили {got!r}"
    if m.psi(REF, SHIFT) <= 0.25:
        return "сдвиг среднего на 2 стандартных отклонения должен давать psi заметно больше 0.25"
    ref = list(range(100))
    cur = [0] * 50
    want = ref_psi(ref, cur, 4)
    got = m.psi(ref, cur, bins=4)
    if not near(got, want):
        return f"все текущие значения в первом бине: ожидалось {want:.9f}, получили {got!r} (пустые бины заменяются на eps)"


def t_ks_basic():
    got = m.ks_statistic([1, 2, 3], [2, 3, 4])
    if not near(got, 1 / 3, 1e-12):
        return f"ks([1, 2, 3], [2, 3, 4]): ожидалось 1/3, получили {got!r}"
    if not near(m.ks_statistic([1, 2], [3, 4]), 1.0, 1e-12):
        return "не пересекающиеся выборки: ожидалось 1.0"
    if not near(m.ks_statistic(REF, REF), 0.0, 1e-12):
        return "выборка с самой собой: ожидалось 0.0"
    for name, cur in (("похожая", SAME), ("сдвиг", SHIFT), ("масштаб", SCALED)):
        got, want = m.ks_statistic(REF, cur), ref_ks(REF, cur)
        if not near(got, want):
            return f"ks ({name}): ожидалось {want:.9f}, получили {got!r}"


def t_report_basic():
    ref = {"age": gauss(10, 1000), "income": gauss(11, 1000), "score": gauss(12, 1000)}
    cur = {"age": gauss(13, 1000), "income": gauss(14, 1000, 2.0), "score": gauss(15, 1000, 0.0, 1.5)}
    want = ref_report(ref, cur)
    got = m.drift_report(ref, cur)
    if [r.get("feature") for r in got] != ["age", "income", "score"]:
        return f"строки отчёта идут по sorted(reference): ожидалось ['age', 'income', 'score'], получили {[r.get('feature') for r in got]!r}"
    if [r.get("status") for r in got] != [r["status"] for r in want]:
        return f"статусы: ожидалось {[r['status'] for r in want]}, получили {[r.get('status') for r in got]}"
    if got != want:
        return f"отчёт не совпал с эталоном: psi и ks округляются до 4 знаков; ожидалось {want}, получили {got}"
    if set(got[0]) != {"feature", "psi", "ks", "status"}:
        return f"в строке отчёта должны быть ключи feature, psi, ks, status; получили {sorted(got[0])}"


# ---------- скрытые ----------

def t_edges_validation():
    for bins in (1, 0, -3):
        try:
            m.quantile_edges([1, 2, 3], bins)
        except ValueError:
            continue
        return f"bins = {bins}: ожидалось ValueError"
    try:
        m.quantile_edges([], 4)
    except ValueError:
        pass
    else:
        return "пустая эталонная выборка: ожидалось ValueError"
    got = m.quantile_edges([1, 2, 3, 4], 2)
    if list(got) != [3]:
        return f"bins = 2 даёт одну границу s[n // 2]: для [1, 2, 3, 4] ожидалось [3], получили {got!r}"
    got = m.quantile_edges([1] * 50 + [2] * 50, 4)
    if list(got) != [1, 2, 2]:
        return f"повторяющиеся значения дают повторяющиеся границы; ожидалось [1, 2, 2], получили {got!r}"
    got = m.quantile_edges([7], 10)
    if list(got) != [7] * 9:
        return f"одно значение и 10 бинов: ожидалось девять семёрок, получили {got!r}"
    data = [3, 1, 2]
    m.quantile_edges(data, 2)
    if data != [3, 1, 2]:
        return "quantile_edges не должна менять переданный список (сортируйте копию)"
    data = gauss(21, 777)
    for bins in (2, 3, 7, 10):
        if list(m.quantile_edges(data, bins)) != ref_edges(data, bins):
            return f"bins = {bins}, 777 значений: границы не совпали с эталоном"
    got = m.bin_fractions([2, 2, 2], [2, 2])
    if len(got) != 3 or not near(got[2], 1.0, 1e-12):
        return f"значение, равное повторяющимся границам [2, 2], попадает в последний бин: получили {got!r}"


def t_psi_edges():
    ref = [1] * 50 + [2] * 50
    cur = [1] * 20 + [2] * 80
    got, want = m.psi(ref, cur, bins=4), ref_psi(ref, cur, 4)
    if not near(got, want):
        return f"много повторов в эталоне (границы [1, 2, 2], пустые бины): ожидалось {want:.9f}, получили {got!r}"
    far = [1000.0 + i for i in range(500)]
    got, want = m.psi(REF, far), ref_psi(REF, far)
    if not near(got, want) or got <= 1.0:
        return f"текущие значения целиком правее эталона (все в последнем бине): ожидалось {want:.6f}, получили {got!r}"
    got, want = m.psi(REF, far, eps=1e-2), ref_psi(REF, far, eps=1e-2)
    if not near(got, want):
        return f"параметр eps влияет на результат: при eps = 0.01 ожидалось {want:.9f}, получили {got!r}"
    try:
        m.psi(REF, [])
    except ValueError:
        pass
    else:
        return "пустая текущая выборка: ожидалось ValueError"
    for bins in (1, 0):
        try:
            m.psi(REF, SAME, bins=bins)
        except ValueError:
            continue
        return f"psi при bins = {bins}: ожидалось ValueError"
    small = [0.5, 1.5, 2.5]
    got, want = m.psi(small, [5.0], bins=3), ref_psi(small, [5.0], 3)
    if not near(got, want):
        return f"малые выборки (3 и 1 значение): ожидалось {want:.9f}, получили {got!r}"
    a, b = m.psi(REF, SHIFT), m.psi(SHIFT, REF)
    if not near(a, ref_psi(REF, SHIFT)) or not near(b, ref_psi(SHIFT, REF)):
        return "psi несимметричен: границы строятся по первому аргументу (эталону)"
    ints = [i % 7 for i in range(700)]
    got, want = m.psi(ints, [i % 5 for i in range(300)], bins=5), ref_psi(ints, [i % 5 for i in range(300)], 5)
    if not near(got, want):
        return f"целочисленные признаки с повторами: ожидалось {want:.9f}, получили {got!r}"


def t_ks_edges():
    a = [1, 1, 1, 2]
    b = [1, 2, 2, 2, 2, 2]
    got, want = m.ks_statistic(a, b), ref_ks(a, b)
    if not near(got, want):
        return f"повторяющиеся значения и разные размеры: ожидалось {want:.9f}, получили {got!r}"
    x, y = gauss(31, 400), gauss(32, 900, 0.3)
    if not near(m.ks_statistic(x, y), m.ks_statistic(y, x)):
        return "статистика симметрична: ks(a, b) == ks(b, a)"
    if not near(m.ks_statistic(x, y), ref_ks(x, y)):
        return f"выборки 400 и 900: ожидалось {ref_ks(x, y):.9f}, получили {m.ks_statistic(x, y)!r}"
    if not near(m.ks_statistic([5], [5]), 0.0, 1e-12) or not near(m.ks_statistic([5], [6]), 1.0, 1e-12):
        return "по одному значению: равные - 0.0, разные - 1.0"
    for p, q in (([], [1]), ([1], []), ([], [])):
        try:
            m.ks_statistic(p, q)
        except ValueError:
            continue
        return f"ks({p}, {q}): пустая выборка, ожидалось ValueError"
    a = [3, 1, 2]
    m.ks_statistic(a, [1])
    if a != [3, 1, 2]:
        return "ks_statistic не должна менять переданные списки"
    if not near(m.ks_statistic([1, 2, 3, 4], [10, 11]), 1.0, 1e-12):
        return "полностью разделённые выборки разного размера: ожидалось 1.0"
    ties = [0] * 10
    if not near(m.ks_statistic(ties, [0] * 3 + [1] * 3), 0.5, 1e-12):
        return "все значения равны 0 против половины нулей и половины единиц: ожидалось 0.5"


def t_report_missing_and_thresholds():
    ref = {"b": gauss(41, 500), "a": gauss(42, 500), "c": gauss(43, 500)}
    cur = {"a": gauss(44, 500, 1.0), "c": []}
    got = m.drift_report(ref, cur)
    want = ref_report(ref, cur)
    if [r["feature"] for r in got] != ["a", "b", "c"]:
        return f"порядок признаков должен быть алфавитным, получили {[r['feature'] for r in got]}"
    if got[1] != {"feature": "b", "psi": None, "ks": None, "status": "missing"}:
        return f"признак отсутствует в current: ожидалось {{'feature': 'b', 'psi': None, 'ks': None, 'status': 'missing'}}, получили {got[1]!r}"
    if got[2] != {"feature": "c", "psi": None, "ks": None, "status": "missing"}:
        return f"пустой список в current тоже означает 'missing', получили {got[2]!r}"
    if got != want:
        return f"отчёт не совпал с эталоном: ожидалось {want}, получили {got}"
    extra = {"a": gauss(45, 500), "zzz": [1, 2, 3]}
    got = m.drift_report({"a": ref["a"]}, extra)
    if [r["feature"] for r in got] != ["a"]:
        return "признаки, которых нет в эталоне, в отчёт не попадают"
    if m.drift_report({}, {"a": [1]}) != []:
        return "пустой эталон - пустой отчёт"
    base = ref_report({"a": REF}, {"a": SHIFT})[0]
    p = ref_psi(REF, SHIFT)
    for warn, alert, status in ((0.1, 0.25, "alert"), (0.1, p + 1e-9, "warn"), (p + 1e-9, p + 2e-9, "ok"), (0.01, p + 1e-9, "warn"), (0.01, p - 1e-9, "alert")):
        got = m.drift_report({"a": REF}, {"a": SHIFT}, psi_warn=warn, psi_alert=alert)[0]["status"]
        if got != status:
            return f"psi_warn = {warn}, psi_alert = {alert}, psi = {p:.9f}: ожидался статус {status!r}, получили {got!r} (границы: psi < warn - ok, psi < alert - warn, иначе alert)"
    got = m.drift_report({"a": REF}, {"a": SAME}, bins=5)[0]
    if got != ref_report({"a": REF}, {"a": SAME}, bins=5)[0]:
        return "параметр bins должен передаваться в psi"
    same = m.drift_report({"a": REF}, {"a": REF})[0]
    if same != {"feature": "a", "psi": 0.0, "ks": 0.0, "status": "ok"}:
        return f"одинаковые выборки: ожидалось psi 0.0, ks 0.0, status ok; получили {same}"
    if base["status"] != "alert":
        return "внутренняя ошибка проверки"


case("edges-and-fractions", t_edges_and_fractions)
case("psi-basic", t_psi_basic)
case("ks-basic", t_ks_basic)
case("report-basic", t_report_basic)
case("edges-validation", t_edges_validation)
case("psi-edges", t_psi_edges)
case("ks-edges", t_ks_edges)
case("report-missing-and-thresholds", t_report_missing_and_thresholds)
