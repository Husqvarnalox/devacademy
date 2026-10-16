import sys


def step(mem, faults, page, frames):
    """One FIFO step; returns the new (memory, faults) without touching the old ones."""
    if page in mem:
        return mem, faults
    if len(mem) == frames:
        mem = mem[1:]
    return mem + (page,), faults + 1


def search(p, length, f):
    refs = []

    def extend(used, ma, fa, mb, fb):
        if len(refs) == length:
            return (fa, fb) if fb > fa else None
        # Pages are numbered in order of first use: relabelling pages never changes fault counts,
        # so the lexicographically smallest anomalous string always has this form.
        for page in range(1, min(used + 1, p) + 1):
            na, nfa = step(ma, fa, page, f)
            nb, nfb = step(mb, fb, page, f + 1)
            refs.append(page)
            res = extend(max(used, page), na, nfa, nb, nfb)
            if res:
                return res
            refs.pop()
        return None

    res = extend(0, (), 0, (), 0)
    return (list(refs), res) if res else None


def main():
    p, length, f = map(int, sys.stdin.read().split())
    found = search(p, length, f)
    if found is None:
        print("NONE")
    else:
        refs, (a, b) = found
        print(" ".join(map(str, refs)))
        print(f"frames {f}: {a}, frames {f + 1}: {b}")


main()
