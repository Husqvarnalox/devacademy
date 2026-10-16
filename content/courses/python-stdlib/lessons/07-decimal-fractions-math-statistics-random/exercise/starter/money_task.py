import random
import secrets
import statistics
from decimal import ROUND_HALF_EVEN, ROUND_HALF_UP, Decimal, InvalidOperation
from fractions import Fraction

CENT = Decimal("0.01")


def parse_money(text):
    """Строка -> Decimal с двумя знаками; иначе ValueError."""
    # TODO
    return None


def round_money(x, mode="half_up"):
    """Округлить Decimal до копеек: 'half_up' или 'half_even'."""
    # TODO
    return None


def line_total(price, qty, discount_percent):
    """price * qty * (100 - скидка) / 100 с одним округлением в конце."""
    # TODO
    return None


def split_bill(total, n):
    """Разделить сумму на n частей в копейках без потери копеек."""
    # TODO
    return None


def vat_included(gross, rate_percent):
    """НДС внутри цены с налогом."""
    # TODO
    return None


def exact_sum(strings):
    """Точная сумма дробей/десятичных строк; результат - строка str(Fraction)."""
    # TODO
    return None


def summary(values):
    """{'mean', 'median', 'stdev', 'pstdev'}, округлено до 6 знаков."""
    # TODO
    return None


def shuffled(items, seed):
    """Воспроизводимо перемешанная копия; глобальный random не трогать."""
    # TODO
    return None


def new_token(nbytes=16):
    """Секретный hex-токен длиной 2 * nbytes."""
    # TODO
    return None
