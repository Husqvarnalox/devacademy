import sys


def safe_sequence(available, allocation, need):
    """Return a list of process indices in a safe order, or None if the state is unsafe."""
    n = len(allocation)
    work = list(available)
    done = [False] * n
    order = []
    while len(order) < n:
        for i in range(n):
            if not done[i] and all(need[i][j] <= work[j] for j in range(len(work))):
                work = [work[j] + allocation[i][j] for j in range(len(work))]
                done[i] = True
                order.append(i)
                break
        else:
            return None
    return order


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
    need = [[maximum[i][j] - allocation[i][j] for j in range(m)] for i in range(n)]
    print("need:")
    for i in range(n):
        print(f"P{i}: " + " ".join(map(str, need[i])))
    order = safe_sequence(available, allocation, need)
    if order is None:
        print("UNSAFE")
    else:
        print("SAFE: " + " ".join(f"P{i}" for i in order))


main()
