import sys


def fixed(num, den, places):
    """num/den, округлённое half-up до places знаков, строкой. Только целые числа."""
    q = (2 * num * 10**places + den) // (2 * den)
    s = str(q).rjust(places + 1, "0")
    return s[:-places] + "." + s[-places:]


def main():
    days = None
    deploys = []  # (deploy_ts, commit_ts, failed, restored_ts)
    for line in sys.stdin.read().splitlines():
        p = line.split()
        if not p:
            continue
        if p[0] == "DAYS":
            days = int(p[1])
        elif p[0] == "D":
            failed = p[3] == "FAIL"
            deploys.append((int(p[1]), int(p[2]), failed, int(p[4]) if failed else 0))
    # TODO: напечатайте пять строк метрик в формате из условия.
    # Подсказка: fixed(числитель, знаменатель, знаков) уже округляет half-up без float.


main()
