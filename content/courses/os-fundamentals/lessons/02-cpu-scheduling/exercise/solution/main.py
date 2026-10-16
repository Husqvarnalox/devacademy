import sys
from collections import deque


def report(procs, segments, finish, start):
    """procs: list of (name, arrival, burst) in input order."""
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


def round_robin(procs, quantum):
    order = sorted(range(len(procs)), key=lambda i: (procs[i][1], i))
    remaining = {p[0]: p[2] for p in procs}
    arrival = {p[0]: p[1] for p in procs}
    nxt = 0
    queue = deque()
    t = 0
    segments, finish, start = [], {}, {}

    def admit(upto):
        nonlocal nxt
        while nxt < len(order) and procs[order[nxt]][1] <= upto:
            queue.append(procs[order[nxt]][0])
            nxt += 1

    admit(t)
    while len(finish) < len(procs):
        if not queue:
            nt = procs[order[nxt]][1]
            segments.append((t, nt, "idle"))
            t = nt
            admit(t)
            continue
        name = queue.popleft()
        start.setdefault(name, t)
        run = min(quantum, remaining[name])
        segments.append((t, t + run, name))
        t += run
        remaining[name] -= run
        admit(t)
        if remaining[name] > 0:
            queue.append(name)
        else:
            finish[name] = t
    return report(procs, segments, finish, start)


def main():
    lines = sys.stdin.read().split("\n")
    quantum, n = map(int, lines[0].split())
    procs = []
    for line in lines[1:1 + n]:
        name, a, b = line.split()
        procs.append((name, int(a), int(b)))
    print("\n".join(round_robin(procs, quantum)))


main()
