import bisect
import math


def quantile_edges(reference, bins):
    """Границы бинов по квантилям эталонной выборки: bins - 1 значение (по отсортированной выборке s длины n: s[i * n // bins])."""
    # TODO: ValueError при bins < 2 и при пустой выборке; затем границы для i = 1 .. bins - 1
    return []


def bin_fractions(values, edges):
    """Доли значений по бинам: номер бина числа x - bisect.bisect_right(edges, x); бинов len(edges) + 1."""
    # TODO: посчитать количество в каждом бине и поделить на len(values); пустой values - одни нули
    return []


def psi(reference, current, bins=10, eps=1e-4):
    """Индекс стабильности популяции между эталонной и текущей выборками."""
    # TODO: границы из reference, доли обеих выборок, нижний порог eps, сумма (a - e) * ln(a / e)
    return 0.0


def ks_statistic(a, b):
    """Статистика Колмогорова-Смирнова: максимальный зазор между эмпирическими функциями распределения."""
    # TODO: перебрать x из sorted(set(a) | set(b)); F(x) = bisect_right(отсортированная выборка, x) / длина
    return 0.0


def drift_report(reference, current, bins=10, psi_warn=0.1, psi_alert=0.25):
    """Отчёт по каждому признаку эталона: psi, ks и статус ok / warn / alert / missing."""
    # TODO: для каждого признака из sorted(reference) собрать словарь {"feature", "psi", "ks", "status"}
    return []
