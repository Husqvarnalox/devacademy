import bisect
import heapq
import itertools


def top_k(items, k, key=None):
    """k наибольших элементов по убыванию."""
    # TODO
    return None


class TaskQueue:
    """Очередь с приоритетами: меньшее число важнее, при равенстве - FIFO."""

    def __init__(self):
        # TODO
        pass

    def push(self, priority, item):
        # TODO
        pass

    def pop(self):
        # TODO
        return None

    def peek(self):
        # TODO
        return None

    def __len__(self):
        # TODO
        return -1


def schedule(tasks):
    """tasks: [(arrival, priority, name)] -> [(время_старта, name)]."""
    # TODO
    return None


def count_in_range(sorted_values, lo, hi):
    """Число элементов отсортированного списка в [lo, hi)."""
    # TODO
    return None
