import heapq


class RunningMedian:
    def __init__(self):
        self._low = []   # max-куча (отрицательные значения)
        self._high = []  # min-куча

    def add(self, x):
        heapq.heappush(self._low, -x)
        heapq.heappush(self._high, -heapq.heappop(self._low))
        if len(self._high) > len(self._low):
            heapq.heappush(self._low, -heapq.heappop(self._high))

    def median(self):
        if not self._low:
            raise ValueError("нет данных")
        if len(self._low) > len(self._high):
            return -self._low[0]
        return (-self._low[0] + self._high[0]) / 2

    def __len__(self):
        return len(self._low) + len(self._high)
