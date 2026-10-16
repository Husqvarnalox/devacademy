import numpy as np
import pandas as pd


def add_cost(df):
    """Копия df со столбцом cost (float64) = load_pct * ставка(state) + 2.0 для site == "warehouse".

    Ставка: ok -> 0.10, warn -> 0.15, fault -> 0.30. БЕЗ apply, iterrows и itertuples - только векторные операции.
    """
    # TODO
    return None


def shrink(df):
    """Новая таблица с меньшей памятью: строковые столбцы, у которых nunique/len <= 0.5, -> category;
    целые столбцы -> наименьший подходящий целый тип (pd.to_numeric(..., downcast="integer")).
    Вещественные и остальные столбцы не менять. Входной df не менять; значения не менять.
    """
    # TODO
    return None


def memory_bytes(df):
    """Полный размер таблицы в байтах, включая содержимое строк (int)."""
    # TODO
    return None


def site_report(df):
    """Цепочкой методов: строки со state == "ok"; temp_f = temp_c * 9 / 5 + 32;
    по site: readings (число строк) и mean_temp_f (среднее, округлить до 2 знаков).
    Столбцы: site, readings, mean_temp_f; строки по site по возрастанию; индекс 0..k-1.
    """
    # TODO
    return None
