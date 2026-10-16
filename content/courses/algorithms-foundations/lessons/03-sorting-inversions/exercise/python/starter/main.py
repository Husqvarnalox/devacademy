import sys


def merge_count(a):
    """Возвращает (отсортированная копия a, число пар i < j с a[i] > a[j])."""
    # TODO: сортировка слиянием, которая заодно считает инверсии (O(n log n))
    return a, 0


def main():
    data = sys.stdin.buffer.read().split()
    n = int(data[0])
    a = list(map(int, data[1:1 + n]))
    print(merge_count(a)[1])


main()
