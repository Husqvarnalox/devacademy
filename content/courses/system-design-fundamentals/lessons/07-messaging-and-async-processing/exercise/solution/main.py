import sys


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    parts_count = int(lines[0])
    log = [[] for _ in range(parts_count)]
    committed = [0] * parts_count
    members = []            # sorted consumer names
    assign = {}             # consumer -> sorted partitions
    position = {}           # partition -> next offset to read
    done = set()            # (partition, offset) already processed at least once
    processed = redelivered = 0

    def rebalance(commit_live):
        nonlocal assign
        if commit_live:
            for p in range(parts_count):
                if any(p in ps for ps in assign.values()):
                    committed[p] = position[p]
        assign = {c: [p for p in range(parts_count) if members and p % len(members) == i]
                  for i, c in enumerate(members)}
        for p in range(parts_count):
            position[p] = committed[p]
        print("REBALANCE " + (" ".join(f"{c}={assign[c]}".replace(" ", "") for c in members) or "-"))

    for p in range(parts_count):
        position[p] = 0
    for line in lines[1:]:
        t = line.split()
        cmd = t[0]
        if cmd == "PRODUCE":
            log[int(t[1])].append(t[2])
        elif cmd == "JOIN":
            members = sorted(members + [t[1]])
            rebalance(True)
        elif cmd == "LEAVE":
            members.remove(t[1])
            rebalance(True)
        elif cmd == "CRASH":
            for p in assign.pop(t[1], []):
                position[p] = committed[p]  # uncommitted progress is lost
            members.remove(t[1])
            rebalance(True)
        elif cmd == "POLL":
            c, budget = t[1], int(t[2])
            for p in assign[c]:
                while budget > 0 and position[p] < len(log[p]):
                    off = position[p]
                    again = (p, off) in done
                    done.add((p, off))
                    processed += 1
                    redelivered += again
                    print(f"{c} p{p}@{off} {log[p][off]}" + (" REDELIVERY" if again else ""))
                    position[p] += 1
                    budget -= 1
        elif cmd == "COMMIT":
            for p in assign[t[1]]:
                committed[p] = position[p]
    print("committed " + " ".join(f"p{p}={committed[p]}" for p in range(parts_count)))
    print("lag " + " ".join(f"p{p}={len(log[p]) - committed[p]}" for p in range(parts_count)))
    print(f"processed={processed} redelivered={redelivered}")


main()
