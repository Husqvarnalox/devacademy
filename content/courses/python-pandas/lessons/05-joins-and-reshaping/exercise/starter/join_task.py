import pandas as pd


def join_orders(orders, customers):
    """Присоедините данные клиента (name, country, segment) к каждому заказу.

    Нужны все заказы (клиента может не быть в customers), порядок заказов сохраняется.
    Если customer_id в customers не уникален, должна возникнуть ошибка pandas.errors.MergeError
    (проверьте связь многие-к-одному средствами merge, а не вручную).
    """
    # TODO
    return None


def find_orphans(orders, customers):
    """Отсортированный список уникальных order_id заказов, чьего клиента нет в customers."""
    # TODO
    return None


def revenue_pivot(joined):
    """Сводная таблица суммы amount: строки - country, столбцы - segment, пустые комбинации - 0.

    На входе результат join_orders. Заказы без страны в таблицу не попадают.
    """
    # TODO
    return None


def unpivot(wide):
    """Обратное преобразование сводной таблицы в «длинный» вид.

    Столбцы: country, segment, revenue. Строки отсортированы по country, затем segment; индекс 0..k-1.
    """
    # TODO
    return None
