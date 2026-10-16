import sys


def merge_count(a):
    """Возвращает (отсортированная копия a, число пар i < j с a[i] > a[j])."""
    if len(a) <= 1:
        return a, 0
    mid = len(a) // 2
    left, inv_l = merge_count(a[:mid])
    right, inv_r = merge_count(a[mid:])
    merged = []
    inv = inv_l + inv_r
    i = j = 0
    while i < len(left) and j < len(right):
        if left[i] <= right[j]:
            merged.append(left[i])
            i += 1
        else:
            merged.append(right[j])
            j += 1
            inv += len(left) - i      # right[j] меньше всех оставшихся в left: столько инверсий
    merged.extend(left[i:])
    merged.extend(right[j:])
    return merged, inv


def main():
    data = sys.stdin.buffer.read().split()
    n = int(data[0])
    a = list(map(int, data[1:1 + n]))
    print(merge_count(a)[1])


main()
