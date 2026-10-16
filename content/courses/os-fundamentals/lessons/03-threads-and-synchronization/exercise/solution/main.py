import sys


def run(init, threads, schedule):
    """Execute one instruction per schedule entry. Return (x, pcs)."""
    x = init
    regs = [0] * len(threads)
    pcs = [0] * len(threads)
    owner = None                      # index of the thread holding the mutex
    for tid in schedule:
        if pcs[tid] >= len(threads[tid]):
            continue                  # finished thread: the step is skipped
        op = threads[tid][pcs[tid]]
        name = op[0]
        if name == "LOCK":
            if owner is not None:
                continue              # mutex is taken: the thread stays on LOCK
            owner = tid
        elif name == "UNLOCK":
            owner = None
        elif name == "LOAD":
            regs[tid] = x
        elif name == "ADD":
            regs[tid] += op[1]
        elif name == "STORE":
            x = regs[tid]
        pcs[tid] += 1
    return x, pcs


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
    serial_schedule = [i for i in range(n) for _ in threads[i]]
    serial, _ = run(init, threads, serial_schedule)
    print(f"x = {x}")
    print(f"serial = {serial}")
    for i in range(n):
        print(f"T{i}: {pcs[i]}/{len(threads[i])}")
    if any(pcs[i] < len(threads[i]) for i in range(n)):
        verdict = "n/a"
    else:
        verdict = "yes" if x != serial else "no"
    print(f"lost update: {verdict}")


main()
