import sys


def fifo(refs, frames):
    mem = []
    faults = 0
    for page in refs:
        if page in mem:
            continue
        faults += 1
        if len(mem) == frames:
            mem.pop(0)
        mem.append(page)
    return faults


def lru(refs, frames):
    mem = []                      # least recently used first
    faults = 0
    for page in refs:
        if page in mem:
            mem.remove(page)
        else:
            faults += 1
            if len(mem) == frames:
                mem.pop(0)
        mem.append(page)
    return faults


def opt(refs, frames):
    mem = set()
    faults = 0
    for i, page in enumerate(refs):
        if page in mem:
            continue
        faults += 1
        if len(mem) == frames:
            def next_use(p):
                for j in range(i + 1, len(refs)):
                    if refs[j] == p:
                        return j
                return len(refs) + 1      # never used again
            victim = max(mem, key=next_use)
            mem.remove(victim)
        mem.add(page)
    return faults


def main():
    lines = sys.stdin.read().split("\n")
    frames = int(lines[0])
    refs = [int(t) for t in lines[1].split()]
    print(f"FIFO {fifo(refs, frames)}")
    print(f"LRU {lru(refs, frames)}")
    print(f"OPT {opt(refs, frames)}")


main()
