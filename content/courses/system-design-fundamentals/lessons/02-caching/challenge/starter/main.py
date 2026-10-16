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
        # TODO: GET - попадание увеличивает freq и обновляет last_used; PUT - новый ключ начинает с freq 1,
        # при полном кэше вытесняется ключ с минимальной (freq, last_used)
    # TODO: напечатайте "hits=... misses=... evictions=..." и "contents: key:freq ..." (ключи по алфавиту)


main()
