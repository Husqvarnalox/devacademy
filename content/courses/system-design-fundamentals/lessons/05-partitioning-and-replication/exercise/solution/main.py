import sys


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    n, w, r = (int(x) for x in lines[0].split())
    reps = [(0, "-") for _ in range(n)]  # (version, value)
    up = [True] * n
    latest = 0  # highest acknowledged version
    for line in lines[1:]:
        parts = line.split()
        cmd = parts[0]
        alive = [i for i in range(n) if up[i]]
        if cmd == "DOWN":
            up[int(parts[1])] = False
        elif cmd == "UP":
            up[int(parts[1])] = True
        elif cmd == "WRITE":
            if len(alive) < w:
                print(f"WRITE {parts[1]} FAIL up={len(alive)}")
                continue
            latest += 1
            for i in alive[:w]:
                reps[i] = (latest, parts[1])
            print(f"WRITE {parts[1]} OK v={latest} acked={alive[:w]}")
        elif cmd == "READ":
            if len(alive) < r:
                print(f"READ FAIL up={len(alive)}")
                continue
            asked = alive[:r]
            ver, val = max((reps[i] for i in asked), key=lambda x: x[0])
            lacking = sum(1 for i in alive if reps[i][0] < latest)
            print(f"READ -> {val} v={ver} from={asked} {'STALE' if ver < latest else 'FRESH'} "
                  f"can_be_stale={'yes' if lacking >= r else 'no'}")
        elif cmd == "SYNC":
            best = max((reps[i] for i in alive), key=lambda x: x[0])
            for i in alive:
                reps[i] = best
            print("SYNC")
        elif cmd == "STATE":
            down = [i for i in range(n) if not up[i]]
            print(f"STATE v={[v for v, _ in reps]} down={down}")


main()
