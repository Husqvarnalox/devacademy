import sys
from collections import OrderedDict


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    capacity, ttl = (int(x) for x in lines[0].split())
    cache = OrderedDict()  # key -> (value, expires_at); first item is the LRU one
    hits = misses = evictions = expirations = 0
    for line in lines[1:]:
        parts = line.split()
        now = int(parts[0])
        for key in [k for k, (_, exp) in cache.items() if exp <= now]:
            del cache[key]
            expirations += 1
        op, key = parts[1], parts[2]
        if op == "GET":
            if key in cache:
                hits += 1
                cache.move_to_end(key)
                print(f"{now} GET {key} HIT {cache[key][0]}")
            else:
                misses += 1
                print(f"{now} GET {key} MISS")
        else:
            value = parts[3]
            if key in cache:
                del cache[key]
            elif len(cache) == capacity:
                cache.popitem(last=False)
                evictions += 1
            cache[key] = (value, now + ttl)
    print(f"hits={hits} misses={misses} evictions={evictions} expirations={expirations}")
    print("keys:" + "".join(" " + k for k in cache))


main()
