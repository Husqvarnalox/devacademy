import sys


def srtf(procs):
    """procs: list of (name, arrival, burst) in input order. Return output lines
    in the same format as the exercise (gantt, table, three averages)."""
    # TODO: simulate time unit by time unit; at every unit pick the arrived process
    # with the smallest remaining time. Do not forget the tie rule from the task.
    return []


def main():
    lines = sys.stdin.read().split("\n")
    n = int(lines[0])
    procs = []
    for line in lines[1:1 + n]:
        name, a, b = line.split()
        procs.append((name, int(a), int(b)))
    print("\n".join(srtf(procs)))


main()
