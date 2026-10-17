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
            windows.append(tuple(int(x) for x in p))
    steps = cfg["steps"]
    floor = Fraction(cfg["ABS_FLOOR_PERMILLE"], 1000)
    idx, state, used = 0, "IN_PROGRESS", 0
    for n, (req_b, err_b, req_c, err_c) in enumerate(windows, 1):
        if state != "IN_PROGRESS":
            break
        used = n
        pct = steps[idx]
        if req_c < cfg["MIN_REQUESTS"]:
            print(f"window {n} step={pct} HOLD")
            continue
        base = Fraction(err_b, req_b) if req_b > 0 else Fraction(0)
        allowed = max(floor, cfg["FACTOR"] * base)
        if Fraction(err_c, req_c) > allowed:
            print(f"window {n} step={pct} ROLLBACK")
            state = "ROLLED_BACK"
        elif idx == len(steps) - 1:
            print(f"window {n} step={pct} COMPLETE")
            state = "COMPLETE"
        else:
            idx += 1
            print(f"window {n} step={pct} PROMOTE next={steps[idx]}")
    weight = {"COMPLETE": steps[-1], "ROLLED_BACK": 0}.get(state, steps[idx])
    print(f"result={state} weight={weight} unused_windows={len(windows) - used}")


main()
