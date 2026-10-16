import random
import secrets
import statistics
from decimal import ROUND_HALF_EVEN, ROUND_HALF_UP, Decimal, InvalidOperation
from fractions import Fraction

CENT = Decimal("0.01")
_MODES = {"half_up": ROUND_HALF_UP, "half_even": ROUND_HALF_EVEN}


def parse_money(text):
    try:
        d = Decimal(text.strip())
        if not d.is_finite():
            raise ValueError("не конечное число")
        q = d.quantize(CENT)
    except InvalidOperation as exc:
        raise ValueError(f"некорректная сумма: {text!r}") from exc
    if q != d:
        raise ValueError("слишком много знаков после запятой")
    return q


def round_money(x, mode="half_up"):
    if mode not in _MODES:
        raise ValueError(f"неизвестный режим {mode!r}")
    return Decimal(x).quantize(CENT, rounding=_MODES[mode])


def line_total(price, qty, discount_percent):
    discount = Decimal(discount_percent)
    if qty < 0 or not (0 <= discount <= 100):
        raise ValueError("некорректное количество или скидка")
    return round_money(Decimal(price) * qty * (100 - discount) / 100)


def split_bill(total, n):
    if n < 1 or total < 0:
        raise ValueError("n должно быть >= 1, сумма >= 0")
    cents = int((Decimal(total) * 100).to_integral_value())
    base, rest = divmod(cents, n)
    return [(Decimal(base + (1 if i < rest else 0)) / 100).quantize(CENT) for i in range(n)]


def vat_included(gross, rate_percent):
    gross = Decimal(gross)
    rate = Decimal(rate_percent)
    return round_money(gross * rate / (100 + rate))


def exact_sum(strings):
    return str(sum((Fraction(s) for s in strings), Fraction(0)))


def summary(values):
    values = list(values)
    if len(values) < 2:
        raise ValueError("нужно хотя бы два значения")
    return {
        "mean": round(statistics.fmean(values), 6),
        "median": round(statistics.median(values), 6),
        "stdev": round(statistics.stdev(values), 6),
        "pstdev": round(statistics.pstdev(values), 6),
    }


def shuffled(items, seed):
    out = list(items)
    random.Random(seed).shuffle(out)
    return out


def new_token(nbytes=16):
    if nbytes < 1:
        raise ValueError("nbytes должно быть >= 1")
    return secrets.token_hex(nbytes)
