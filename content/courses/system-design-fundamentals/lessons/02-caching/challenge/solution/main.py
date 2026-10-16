import sys


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    capacity = int(lines[0])
    store = {}  # key -> [value, freq, last_used]
    clock = 0
    hits = misses = evictions = 0
    for line in lines[1:]:
        parts = line.split()
        clock += 1
        op, key = parts[0], parts[1]
        if op == "GET":
            if key in store:
                hits += 1
                store[key][1] += 1
                store[key][2] = clock
                print(f"GET {key} HIT {store[key][0]}")
            else:
                misses += 1
                print(f"GET {key} MISS")
        else:
            value = parts[2]
            if key in store:
                store[key] = [value, store[key][1] + 1, clock]
            else:
                if len(store) == capacity:
                    victim = min(store, key=lambda k: (store[k][1], store[k][2]))
                    del store[victim]
                    evictions += 1
                store[key] = [value, 1, clock]
    print(f"hits={hits} misses={misses} evictions={evictions}")
    print("contents:" + "".join(f" {k}:{store[k][1]}" for k in sorted(store)))


main()
