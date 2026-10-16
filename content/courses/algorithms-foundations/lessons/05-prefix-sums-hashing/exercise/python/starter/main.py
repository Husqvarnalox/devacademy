import sys


def count_segments(a, k):
    """Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными)."""
    # TODO: префиксные суммы + словарь "сумма -> сколько раз встречалась", O(n)
    return 0


def main():
    data = sys.stdin.buffer.read().split()
    n, k = int(data[0]), int(data[1])
    a = list(map(int, data[2:2 + n]))
    print(count_segments(a, k))


main()
