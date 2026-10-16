import sys
from collections import deque


def report(procs, segments, finish, start):
    """procs: list of (name, arrival, burst) in input order.
    segments: list of (start, end, name) in time order (name may be "idle").
    finish / start: dicts name -> completion time / first time on the CPU."""
    # TODO: merge neighbouring segments with the same name that touch each other,
    # print the gantt chart, the table and the three averages (two decimals).
    return []


def round_robin(procs, quantum):
    # TODO: simulate Round Robin and call report(...).
    # Rule: processes that arrive while a slice is running (or exactly when it ends)
    # enter the queue BEFORE the preempted process is put back at the end.
    return []


def main():
    lines = sys.stdin.read().split("\n")
    quantum, n = map(int, lines[0].split())
    procs = []
    for line in lines[1:1 + n]:
        name, a, b = line.split()
        procs.append((name, int(a), int(b)))
    print("\n".join(round_robin(procs, quantum)))


main()
