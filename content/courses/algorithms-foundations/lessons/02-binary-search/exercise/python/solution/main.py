import sys


def pieces(a, length):
    """Сколько кусков длины length получится из всех досок."""
    return sum(x // length for x in a)


def longest_piece(a, k):
    """Наибольшая целая длина L >= 1, при которой выходит не менее k кусков; 0, если такой L нет."""
    lo, hi = 0, max(a) + 1      # инвариант: lo подходит (0 - условно), hi не подходит
    while hi - lo > 1:
        mid = (lo + hi) // 2
        if pieces(a, mid) >= k:
            lo = mid
        else:
            hi = mid
    return lo


def main():
    data = sys.stdin.buffer.read().split()
    n, k = int(data[0]), int(data[1])
    a = list(map(int, data[2:2 + n]))
    print(longest_piece(a, k))


main()
