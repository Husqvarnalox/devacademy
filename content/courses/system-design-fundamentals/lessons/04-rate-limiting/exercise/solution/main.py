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
        periods = (now - last) // period
        if periods > 0:
            tokens = min(capacity, tokens + periods * refill)
            last += periods * period
        if tokens >= cost:
            tokens -= cost
            allowed += 1
            print(f"{now} ALLOW tokens_left={tokens}")
        else:
            denied += 1
            print(f"{now} DENY tokens_left={tokens}")
    print(f"allowed={allowed} denied={denied}")


main()
