import sys
from collections import OrderedDict


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    capacity, ttl = (int(x) for x in lines[0].split())
    cache = OrderedDict()  # key -> (value, expires_at); первый элемент - самый давний (LRU)
    hits = misses = evictions = expirations = 0
    for line in lines[1:]:
        parts = line.split()
        now = int(parts[0])
        # TODO: 1) удалите просроченные записи (expires_at <= now), считая expirations
        # TODO: 2) GET: попадание освежает запись и печатает "<t> GET <key> HIT <value>", иначе MISS
        # TODO: 3) PUT: при новом ключе и полном кэше вытесните самую давнюю запись (evictions)
    # TODO: напечатайте итоговые строки "hits=... misses=... evictions=... expirations=..." и "keys: ..."


main()
