import sys


def report(procs, segments, finish, start):
    merged = []
    for s, e, name in segments:
        if merged and merged[-1][2] == name and merged[-1][1] == s:
            merged[-1] = (merged[-1][0], e, name)
        else:
            merged.append((s, e, name))
    out = ["gantt:"]
    out += [f"{s}-{e} {name}" for s, e, name in merged]
    out.append("table:")
    tw = tt = tr = 0
    for name, arrival, burst in procs:
        turnaround = finish[name] - arrival
        waiting = turnaround - burst
        response = start[name] - arrival
        tw += waiting
        tt += turnaround
        tr += response
        out.append(f"{name} {waiting} {turnaround} {response}")
    n = len(procs)
    out.append(f"average waiting: {tw / n:.2f}")
    out.append(f"average turnaround: {tt / n:.2f}")
    out.append(f"average response: {tr / n:.2f}")
    return out


def srtf(procs):
    remaining = [p[2] for p in procs]
    t = 0
    segments, finish, start = [], {}, {}
    while len(finish) < len(procs):
        ready = [i for i, p in enumerate(procs) if p[1] <= t and remaining[i] > 0]
        if not ready:
            segments.append((t, t + 1, "idle"))
            t += 1
            continue
        # smaller remaining time wins; ties: earlier arrival, then input order
        best = min(ready, key=lambda i: (remaining[i], procs[i][1], i))
        name = procs[best][0]
        start.setdefault(name, t)
        segments.append((t, t + 1, name))
        remaining[best] -= 1
        t += 1
        if remaining[best] == 0:
            finish[name] = t
    return report(procs, segments, finish, start)


def main():
    lines = sys.stdin.read().split("\n")
    n = int(lines[0])
    procs = []
    for line in lines[1:1 + n]:
        name, a, b = line.split()
        procs.append((name, int(a), int(b)))
    print("\n".join(srtf(procs)))


main()
