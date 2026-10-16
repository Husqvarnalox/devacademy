import sys


def run(init, threads, schedule):
    """threads: list of programs, a program is a list of tuples like ("LOAD",), ("ADD", 1),
    ("STORE",), ("LOCK",), ("UNLOCK",). schedule: list of thread indices, one instruction each.
    Return (final x, list of program counters)."""
    # TODO: keep x, one register and one program counter per thread and the owner of the mutex.
    # A step for a finished thread does nothing; LOCK on a taken mutex does nothing (the thread stays on it).
    return init, [0] * len(threads)


def parse_thread(line):
    prog = []
    for part in line.split(";"):
        words = part.split()
        if not words:
            continue
        prog.append((words[0], int(words[1])) if len(words) > 1 else (words[0],))
    return prog


def main():
    lines = sys.stdin.read().split("\n")
    init = int(lines[0])
    n = int(lines[1])
    threads = [parse_thread(lines[2 + i]) for i in range(n)]
    schedule = [int(w) for w in lines[2 + n].split()]
    x, pcs = run(init, threads, schedule)
    # TODO: compute "serial" = result of running thread 0 completely, then thread 1, ...
    # (use run with a suitable schedule), then print the report from the task text.
    print("not implemented")


main()
