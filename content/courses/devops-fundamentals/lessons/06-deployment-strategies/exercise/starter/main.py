import sys
from fractions import Fraction


def main():
    cfg, windows = {}, []
    for line in sys.stdin.read().splitlines():
        p = line.split()
        if not p:
            continue
        if p[0] == "STEPS":
            cfg["steps"] = [int(x) for x in p[1:]]
        elif p[0] in ("MIN_REQUESTS", "ABS_FLOOR_PERMILLE", "FACTOR"):
            cfg[p[0]] = int(p[1])
        else:
            windows.append(tuple(int(x) for x in p))  # req_base, err_base, req_canary, err_canary
    steps = cfg["steps"]
    # TODO: пройдите по окнам, принимая решение HOLD / ROLLBACK / PROMOTE / COMPLETE,
    #       и напечатайте итог "result=... weight=... unused_windows=..."


main()
