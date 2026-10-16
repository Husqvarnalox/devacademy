import sys


def best_segment(a):
    """Максимальная сумма непустого непрерывного отрезка массива a."""
    # TODO: верните максимальную сумму непустого отрезка за один проход (O(n))
    return 0


def main():
    data = sys.stdin.buffer.read().split()
    n = int(data[0])
    a = list(map(int, data[1:1 + n]))
    print(best_segment(a))


main()
