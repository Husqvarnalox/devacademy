import sys


def count_segments(a, k):
    """Число непустых непрерывных отрезков a с суммой ровно k (числа могут быть отрицательными)."""
    seen = {0: 1}        # префиксная сумма -> сколько раз встречалась; пустой префикс даёт сумму 0
    cur = 0
    ans = 0
    for x in a:
        cur += x
        ans += seen.get(cur - k, 0)   # левые границы, для которых сумма отрезка ровно k
        seen[cur] = seen.get(cur, 0) + 1
    return ans


def main():
    data = sys.stdin.buffer.read().split()
    n, k = int(data[0]), int(data[1])
    a = list(map(int, data[2:2 + n]))
    print(count_segments(a, k))


main()
