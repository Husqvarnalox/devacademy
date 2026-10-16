import heapq

_END = object()


def merge_sorted(*iterables):
    """Лениво сливает отсортированные потоки в один отсортированный."""
    heap = []
    for index, it in enumerate(map(iter, iterables)):
        first = next(it, _END)
        if first is not _END:
            heapq.heappush(heap, (first, index, it))
    while heap:
        value, index, it = heapq.heappop(heap)
        yield value
        following = next(it, _END)
        if following is not _END:
            heapq.heappush(heap, (following, index, it))
