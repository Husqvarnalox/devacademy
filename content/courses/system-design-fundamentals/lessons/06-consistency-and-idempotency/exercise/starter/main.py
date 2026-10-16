import sys


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    limit = int(lines[0])
    charged = {}  # счёт -> сумма успешных списаний
    seen = {}     # ключ идемпотентности -> (счёт, сумма, текст исхода)
    charges = 0
    for n, line in enumerate(lines[1:], start=1):
        key, account, amount = line.split()
        amount = int(amount)
        # TODO: определите исход запроса (OK / DECLINED / INVALID / REPLAY / CONFLICT) и напечатайте его
    # TODO: итог по счетам (ACCOUNT ...) и "charges=N"


main()
