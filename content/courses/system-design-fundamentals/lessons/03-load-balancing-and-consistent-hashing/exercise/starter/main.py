import bisect
import hashlib
import sys


def h(text):
    """Фиксированная хеш-функция: первые 8 hex-символов md5 как целое число."""
    return int(hashlib.md5(text.encode()).hexdigest()[:8], 16)


class Ring:
    def __init__(self, vnodes):
        self.vnodes = vnodes
        self.points = []  # отсортированный список (хеш, узел)

    def add(self, node):
        # TODO: добавьте vnodes точек h(f"{node}#{i}")
        pass

    def remove(self, node):
        # TODO
        pass

    def route(self, key):
        # TODO: первая точка с хешем >= h(key) по часовой стрелке (с переходом через конец); None для пустого кольца
        return None


def main():
    lines = [ln for ln in sys.stdin.read().splitlines() if ln.strip()]
    ring = Ring(int(lines[0]))
    for line in lines[1:]:
        parts = line.split()
        # TODO: TRACK / ROUTE / ADD / REMOVE / LOAD по условию


main()
