import sys


def max_in_window(times, window):
    best = 0
    for t in times:
        best = max(best, sum(1 for u in times if t - window < u <= t))
    return best


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    limit, window = (int(x) for x in lines[0].split())
    fixed = {}      # window index -> allowed count
    swc = {}        # window index -> allowed count
    fixed_ok, swc_ok = [], []
    for line in lines[1:]:
        t = int(line.split()[0])
        w, off = divmod(t, window)
        # fixed window
        f_allow = fixed.get(w, 0) < limit
        if f_allow:
            fixed[w] = fixed.get(w, 0) + 1
            fixed_ok.append(t)
        # sliding window counter
        est = swc.get(w - 1, 0) * (window - off) // window + swc.get(w, 0)
        s_allow = est < limit
        if s_allow:
            swc[w] = swc.get(w, 0) + 1
            swc_ok.append(t)
        print(f"{t} FIXED:{'ALLOW' if f_allow else 'DENY'} SWC:{'ALLOW' if s_allow else 'DENY'}")
    print(f"fixed_allowed={len(fixed_ok)} swc_allowed={len(swc_ok)}")
    print(f"fixed_max_in_window={max_in_window(fixed_ok, window)} swc_max_in_window={max_in_window(swc_ok, window)}")


main()
