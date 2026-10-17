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


def valid(rec):
    if not isinstance(rec, dict):
        return False
    status, lat = rec.get("status"), rec.get("latency_ms")
    for v in (status, lat):
        if isinstance(v, bool) or not isinstance(v, int):
            return False
    return 100 <= status <= 599 and lat >= 0


def percentile(sorted_values, p):
    n = len(sorted_values)
    rank = -(-p * n // 100)  # ceil(p * n / 100) в целых числах
    return sorted_values[max(rank, 1) - 1]


def main():
    lines = [l for l in sys.stdin.read().splitlines() if l.strip()]
    slo_text = lines[0].split()[1]
    slo = Fraction(slo_text) / 100
    records, malformed = [], 0
    for line in lines[1:]:
        try:
            rec = json.loads(line)
        except ValueError:
            malformed += 1
            continue
        if valid(rec):
            records.append(rec)
        else:
            malformed += 1
    n = len(records)
    print(f"requests={n} malformed={malformed}")
    if n == 0:
        print("status=NO_DATA")
        return
    lat = sorted(r["latency_ms"] for r in records)
    print(f"latency_ms p50={percentile(lat, 50)} p95={percentile(lat, 95)} p99={percentile(lat, 99)}")
    bad = sum(1 for r in records if r["status"] >= 500)
    print(f"availability={fixed(Fraction(n - bad, n) * 100, 3)}%")
    budget = n * (1 - slo)
    if budget == 0:
        consumed = "0.00" if bad == 0 else "inf"
    else:
        consumed = fixed(Fraction(bad) / budget * 100, 2)
    print(f"slo={slo_text}% budget_requests={fixed(budget, 2)} bad={bad} consumed={consumed}% remaining={fixed(budget - bad, 2)}")
    print("status=" + ("OK" if bad <= budget else "BUDGET_EXHAUSTED"))


main()
