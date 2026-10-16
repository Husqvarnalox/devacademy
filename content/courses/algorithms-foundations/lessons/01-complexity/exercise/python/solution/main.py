import sys


def best_segment(a):
    """Максимальная сумма непустого непрерывного отрезка массива a."""
    best = cur = a[0]
    for x in a[1:]:
        cur = max(x, cur + x)  # либо продолжаем лучший отрезок, оканчивающийся в предыдущей позиции, либо начинаем заново
        best = max(best, cur)
    return best


def main():
    data = sys.stdin.buffer.read().split()
    n = int(data[0])
    a = list(map(int, data[1:1 + n]))
    print(best_segment(a))


main()
