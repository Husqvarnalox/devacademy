import json
import sys
from fractions import Fraction


def fixed(value, places):
    """Fraction -> строка с places знаками, округление half-up по модулю; знак минус сохраняется."""
    scaled = abs(value) * 10**places
    q = int(scaled + Fraction(1, 2))  # scaled >= 0, поэтому int() = floor
    s = str(q).rjust(places + 1, "0")
    text = s[:-places] + "." + s[-places:] if places else s
    return "-" + text if value < 0 and q != 0 else text


def main():
    lines = [l for l in sys.stdin.read().splitlines() if l.strip()]
    slo_text = lines[0].split()[1]          # например "99.9"
    slo = Fraction(slo_text) / 100          # точная доля: 999/1000
    records, malformed = [], 0
    for line in lines[1:]:
        # TODO: разберите JSON; корректная запись - объект с целыми status (100-599) и latency_ms (>= 0).
        #       Остальное считайте malformed (внимание: в Python bool - тоже int).
        pass
    # TODO: напечатайте пять строк отчёта (см. условие), используя nearest-rank для перцентилей


main()
