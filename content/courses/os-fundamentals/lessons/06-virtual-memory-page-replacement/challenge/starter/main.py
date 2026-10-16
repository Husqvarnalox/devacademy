import sys


def fifo_faults(refs, frames):
    """Number of FIFO page faults (you can reuse your solution from the exercise)."""
    # TODO
    return 0


def search(p, length, f):
    """Return the lexicographically smallest reference string (list of ints from 1..p) of the given
    length with fifo_faults(s, f + 1) > fifo_faults(s, f), or None if there is no such string."""
    # TODO: trying all p**length strings is far too slow. Hint: renaming pages does not change fault counts.
    return None


def main():
    p, length, f = map(int, sys.stdin.read().split())
    found = search(p, length, f)
    if found is None:
        print("NONE")
    else:
        print(" ".join(map(str, found)))
        print(f"frames {f}: {fifo_faults(found, f)}, frames {f + 1}: {fifo_faults(found, f + 1)}")


main()
