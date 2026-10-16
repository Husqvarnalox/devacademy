import sys


def count_segments(a, limit):
    """Число непустых непрерывных отрезков a с суммой не больше limit (все a[i] > 0)."""
    total = 0
    left = 0          # окно a[left..right]; инвариант: его сумма cur <= limit (или окно пусто)
    cur = 0
    for right, x in enumerate(a):
        cur += x
        while cur > limit:
            cur -= a[left]
            left += 1
        total += right - left + 1   # подходящие отрезки, оканчивающиеся в right: начала left..right
    return total


def main():
    data = sys.stdin.buffer.read().split()
    n, limit = int(data[0]), int(data[1])
    a = list(map(int, data[2:2 + n]))
    print(count_segments(a, limit))


main()
