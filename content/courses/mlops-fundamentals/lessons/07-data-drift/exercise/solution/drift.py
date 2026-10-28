import bisect
import math


def quantile_edges(reference, bins):
    """Границы бинов по квантилям эталонной выборки: bins - 1 значение."""
    if bins < 2:
        raise ValueError(f"bins должно быть не меньше 2, получено {bins}")
    if len(reference) == 0:
        raise ValueError("эталонная выборка пуста")
    s = sorted(reference)
    n = len(s)
    return [s[(i * n) // bins] for i in range(1, bins)]


def bin_fractions(values, edges):
    """Доли значений по бинам; бин числа x - bisect_right(edges, x)."""
    counts = [0] * (len(edges) + 1)
    for x in values:
        counts[bisect.bisect_right(edges, x)] += 1
    total = len(values)
    if total == 0:
        return [0.0] * len(counts)
    return [c / total for c in counts]


def psi(reference, current, bins=10, eps=1e-4):
    """Индекс стабильности популяции между эталонной и текущей выборками."""
    edges = quantile_edges(reference, bins)
    if len(current) == 0:
        raise ValueError("текущая выборка пуста")
    expected = bin_fractions(reference, edges)
    actual = bin_fractions(current, edges)
    total = 0.0
    for e, a in zip(expected, actual):
        e = max(e, eps)
        a = max(a, eps)
        total += (a - e) * math.log(a / e)
    return total


def ks_statistic(a, b):
    """Статистика Колмогорова-Смирнова: максимальный зазор между эмпирическими функциями распределения."""
    if len(a) == 0 or len(b) == 0:
        raise ValueError("обе выборки должны быть непустыми")
    sa, sb = sorted(a), sorted(b)
    best = 0.0
    for x in sorted(set(sa) | set(sb)):
        gap = abs(bisect.bisect_right(sa, x) / len(sa) - bisect.bisect_right(sb, x) / len(sb))
        if gap > best:
            best = gap
    return best


def drift_report(reference, current, bins=10, psi_warn=0.1, psi_alert=0.25):
    """Отчёт по каждому признаку эталона: psi, ks и статус ok / warn / alert / missing."""
    rows = []
    for feature in sorted(reference):
        cur = current.get(feature)
        if not cur:
            rows.append({"feature": feature, "psi": None, "ks": None, "status": "missing"})
            continue
        p = psi(reference[feature], cur, bins)
        k = ks_statistic(reference[feature], cur)
        status = "ok" if p < psi_warn else "warn" if p < psi_alert else "alert"
        rows.append({"feature": feature, "psi": round(p, 4), "ks": round(k, 4), "status": status})
    return rows
