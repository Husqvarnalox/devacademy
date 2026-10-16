import pandas as pd


def load_orders(path):
    """Прочитайте CSV заказов.

    Нужно: order_date - datetime; sku - строка (ведущие нули важны); маркеры пропусков "N/A" и "-" - это NaN.
    """
    # TODO
    return None


def missing_report(df):
    """Series: число пропусков по столбцам, только столбцы с пропусками, по убыванию."""
    # TODO
    return None


def clean_orders(df):
    """Верните очищенную копию (df не менять):
    убрать полные дубликаты (оставить первую строку), убрать строки без qty или unit_price,
    qty привести к int64, индекс сделать 0..n-1.
    """
    # TODO
    return None


def fill_price(df):
    """Верните копию df, где пропуски unit_price заполнены МЕДИАНОЙ цены того же product."""
    # TODO
    return None
