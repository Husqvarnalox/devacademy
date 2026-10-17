import operator
import sys

OPS = {">": operator.gt, ">=": operator.ge, "<": operator.lt, "<=": operator.le}


def main():
    lines = [l.split() for l in sys.stdin.read().splitlines() if l.strip()]
    _, name, op, threshold, _, hold = lines[0]
    cmp, threshold, hold = OPS[op], int(threshold), int(hold)
    ticks = {}
    for t, series, value in lines[1:]:
        ticks.setdefault(int(t), {})[series] = int(value)
    state, since = {}, {}
    for t in sorted(ticks):
        for s in sorted(set(state) | set(ticks[t])):
            st = state.get(s, "inactive")
            cond = s in ticks[t] and cmp(ticks[t][s], threshold)
            if cond:
                if st == "inactive":
                    print(f"{t} {s} inactive->pending")
                    st, since[s] = "pending", t
                if st == "pending" and t - since[s] >= hold:
                    print(f"{t} {s} pending->firing")
                    st = "firing"
            elif st != "inactive":
                print(f"{t} {s} {st}->inactive")
                st = "inactive"
            state[s] = st
    for st in ("firing", "pending"):
        names = sorted(s for s, v in state.items() if v == st)
        print(f"{st}: " + (",".join(names) or "-"))


main()
