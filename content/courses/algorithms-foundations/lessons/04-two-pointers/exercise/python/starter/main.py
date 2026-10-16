import sys


def count_segments(a, limit):
    """Число непустых непрерывных отрезков a с суммой не больше limit (все a[i] > 0)."""
    # TODO: два указателя (скользящее окно), O(n)
    return 0


def main():
    data = sys.stdin.buffer.read().split()
    n, limit = int(data[0]), int(data[1])
    a = list(map(int, data[2:2 + n]))
    print(count_segments(a, limit))


main()
