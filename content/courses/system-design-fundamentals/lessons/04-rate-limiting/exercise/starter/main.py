import sys


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    capacity, refill, period = (int(x) for x in lines[0].split())
    tokens = capacity
    last = 0
    allowed = denied = 0
    for line in lines[1:]:
        parts = line.split()
        now = int(parts[0])
        cost = int(parts[1]) if len(parts) > 1 else 1
        # TODO: пополните ведро за прошедшие целые периоды, затем разрешите или отклоните запрос
        # и напечатайте "<t> ALLOW tokens_left=N" или "<t> DENY tokens_left=N"
    # TODO: напечатайте "allowed=... denied=..."


main()
