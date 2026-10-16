import pandas as pd


def select_orders(df, regions, min_qty):
    """Строки, где region входит в regions, qty >= min_qty и status не равен "cancelled".

    Порядок строк сохранить. Верните независимый DataFrame.
    """
    # TODO
    return None


def bulk_discount(df):
    """Верните копию df, где unit_price умножена на 0.9 у строк со status == "shipped" и qty >= 10.

    Исходный df менять нельзя.
    """
    # TODO
    return None


def cancel_orders(df, ids):
    """Верните копию df, где status = "cancelled" у заказов, чей order_id входит в ids. Исходный df не менять."""
    # TODO
    return None


def expensive(df, threshold):
    """DataFrame со столбцами order_id и total (= qty * unit_price) только для заказов с total СТРОГО больше threshold.

    Порядок строк сохранить. Исходный df не менять.
    """
    # TODO
    return None
