import pandas as pd


def zscore_within_region(df):
    """Series (индекс как у df): (revenue - среднее региона) / std региона (ddof=1), NaN-регион - своя группа.

    Для группы из одной строки std не определено - результат NaN.
    """
    # TODO
    return None


def top_n_per_region(df, n):
    """Топ-n продаж по revenue в каждом регионе (включая NaN-регион).

    Столбцы: sale_id, region, revenue. При равной выручке раньше идёт меньший sale_id.
    Порядок: region по возрастанию (NaN последним), внутри региона revenue по убыванию. Индекс 0..k-1.
    """
    # TODO
    return None
