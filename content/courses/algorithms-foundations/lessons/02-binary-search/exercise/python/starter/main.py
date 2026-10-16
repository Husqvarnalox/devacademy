import sys


def pieces(a, length):
    """Сколько кусков длины length получится из всех досок."""
    return sum(x // length for x in a)


def longest_piece(a, k):
    """Наибольшая целая длина L >= 1, при которой выходит не менее k кусков; 0, если такой L нет."""
    # TODO: бинарный поиск по ответу - длине куска (O(n log max(a)))
    return 0


def main():
    data = sys.stdin.buffer.read().split()
    n, k = int(data[0]), int(data[1])
    a = list(map(int, data[2:2 + n]))
    print(longest_piece(a, k))


main()
