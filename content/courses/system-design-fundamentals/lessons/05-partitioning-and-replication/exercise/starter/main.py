import sys


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    n, w, r = (int(x) for x in lines[0].split())
    reps = [(0, "-") for _ in range(n)]  # (версия, значение) каждой реплики
    up = [True] * n
    latest = 0  # самая большая подтверждённая версия
    for line in lines[1:]:
        parts = line.split()
        # TODO: DOWN / UP / WRITE / READ / SYNC / STATE по условию


main()
