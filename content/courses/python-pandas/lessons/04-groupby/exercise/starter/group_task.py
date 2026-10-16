import pandas as pd


def region_summary(df):
    """Сводка по регионам (индекс - region, строки с пропущенным регионом ТОЖЕ образуют группу NaN).

    Столбцы по порядку: orders (число продаж, по sale_id), units (сумма qty),
    revenue (сумма revenue), avg_ticket (средний revenue). Строки по убыванию revenue.
    """
    # TODO
    return None


def add_share(df):
    """Копия df с новым столбцом region_share: revenue строки / суммарный revenue её региона.

    Индекс сохранить; строки с пропущенным регионом считаются одной группой.
    """
    # TODO
    return None


def top_product(df):
    """Series: для каждого региона (включая NaN) продукт с наибольшей суммарной выручкой.

    Индекс - region (по возрастанию, NaN последним), имя Series - "product".
    """
    # TODO
    return None
