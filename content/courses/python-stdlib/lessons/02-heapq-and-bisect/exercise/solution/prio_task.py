import bisect
import heapq
import itertools


def top_k(items, k, key=None):
    if k <= 0:
        return []
    return heapq.nlargest(k, items, key=key)


class TaskQueue:
    def __init__(self):
        self._heap = []
        self._counter = itertools.count()

    def push(self, priority, item):
        heapq.heappush(self._heap, (priority, next(self._counter), item))

    def pop(self):
        return heapq.heappop(self._heap)[2]

    def peek(self):
        return self._heap[0][2]

    def __len__(self):
        return len(self._heap)


def schedule(tasks):
    order = sorted(range(len(tasks)), key=lambda i: tasks[i][0])
    heap = []
    result = []
    pos = 0
    t = tasks[order[0]][0] if order else 0
    while pos < len(order) or heap:
        while pos < len(order) and tasks[order[pos]][0] <= t:
            i = order[pos]
            heapq.heappush(heap, (tasks[i][1], i, tasks[i][2]))
            pos += 1
        if not heap:
            t = tasks[order[pos]][0]
            continue
        _, _, name = heapq.heappop(heap)
        result.append((t, name))
        t += 1
    return result


def count_in_range(sorted_values, lo, hi):
    if hi <= lo:
        return 0
    return bisect.bisect_left(sorted_values, hi) - bisect.bisect_left(sorted_values, lo)
