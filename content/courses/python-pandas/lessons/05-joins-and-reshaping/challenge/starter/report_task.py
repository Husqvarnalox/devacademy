import pandas as pd


def customer_report(orders, customers):
    """Одна строка на КЛИЕНТА из customers (в том числе без заказов).

    Столбцы: customer_id, country, orders (число заказов, целое), revenue (сумма amount, float; 0.0 если заказов нет).
    Заказы клиентов, которых нет в customers, в отчёт не входят. Порядок: revenue по убыванию, при равенстве customer_id по возрастанию; индекс 0..k-1.
    """
    # TODO
    return None


def merge_summary(orders, customers):
    """Словарь {"both": ..., "orders_only": ..., "customers_only": ...}.

    both - число заказов, у которых есть клиент; orders_only - число заказов без клиента;
    customers_only - число КЛИЕНТОВ без единого заказа. Все значения - обычные int.
    """
    # TODO
    return None
