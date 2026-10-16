import bisect
import hashlib
import sys


def h(text):
    return int(hashlib.md5(text.encode()).hexdigest()[:8], 16)


class Ring:
    def __init__(self, vnodes):
        self.vnodes = vnodes
        self.points = []  # sorted list of (hash, node)

    def add(self, node):
        for i in range(self.vnodes):
            bisect.insort(self.points, (h(f"{node}#{i}"), node))

    def remove(self, node):
        self.points = [p for p in self.points if p[1] != node]

    def route(self, key):
        if not self.points:
            return None
        i = bisect.bisect_left(self.points, (h(key), ""))
        if i == len(self.points):
            i = 0
        return self.points[i][1]


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    ring = Ring(int(lines[0]))
    tracked = []
    owners = {}
    for line in lines[1:]:
        parts = line.split()
        cmd = parts[0]
        if cmd == "TRACK":
            for k in parts[1:]:
                if k not in tracked:
                    tracked.append(k)
                    owners[k] = ring.route(k)
        elif cmd == "ROUTE":
            print(f"{parts[1]} -> {ring.route(parts[1]) or 'NONE'}")
        elif cmd in ("ADD", "REMOVE"):
            before = dict(owners)
            (ring.add if cmd == "ADD" else ring.remove)(parts[1])
            owners = {k: ring.route(k) for k in tracked}
            moved = sorted(k for k in tracked if before[k] is not None and before[k] != owners[k])
            print(f"{cmd} {parts[1]} moved={len(moved)} keys={','.join(moved) or '-'}")
        elif cmd == "LOAD":
            counts = {}
            for k in tracked:
                if owners[k] is not None:
                    counts[owners[k]] = counts.get(owners[k], 0) + 1
            nodes = sorted({n for _, n in ring.points})
            print("LOAD" + "".join(f" {n}={counts.get(n, 0)}" for n in nodes))


main()
