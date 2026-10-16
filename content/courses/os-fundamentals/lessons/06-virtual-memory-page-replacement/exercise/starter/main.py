import sys


def fifo(refs, frames):
    """Number of page faults of FIFO replacement with `frames` frames."""
    # TODO
    return 0


def lru(refs, frames):
    """Number of page faults of LRU replacement."""
    # TODO
    return 0


def opt(refs, frames):
    """Number of page faults of the optimal (Belady) policy: evict the page whose next use is farthest in the future."""
    # TODO
    return 0


def main():
    lines = sys.stdin.read().split("\n")
    frames = int(lines[0])
    refs = [int(t) for t in lines[1].split()]
    print(f"FIFO {fifo(refs, frames)}")
    print(f"LRU {lru(refs, frames)}")
    print(f"OPT {opt(refs, frames)}")


main()
