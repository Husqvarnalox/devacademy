import sys


def safe_sequence(available, allocation, need):
    """Return a list of process indices in a safe order, or None if the state is unsafe.
    Rule: always take the process with the LOWEST index whose need fits into work, then restart the scan."""
    # TODO: work = available; repeat: find an unfinished i with need[i] <= work (component-wise),
    # finish it (work += allocation[i]) and append i to the order.
    return None


def main():
    tokens = sys.stdin.read().split()
    pos = 0

    def take(k):
        nonlocal pos
        vals = [int(t) for t in tokens[pos:pos + k]]
        pos += k
        return vals

    n, m = take(2)
    available = take(m)
    allocation = [take(m) for _ in range(n)]
    maximum = [take(m) for _ in range(n)]
    # TODO: need[i][j] = maximum[i][j] - allocation[i][j]; print the "need:" block, then the verdict.
    print("UNSAFE")


main()
