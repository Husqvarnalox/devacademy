import itertools
import operator
from functools import cache, cmp_to_key, reduce, singledispatch


def diffs(numbers):
    return [b - a for a, b in itertools.pairwise(numbers)]


def batches(iterable, n):
    return list(itertools.batched(iterable, n))


def combos(items, r):
    return list(itertools.combinations(items, r))


def largest_number(nums):
    def cmp(a, b):
        if a + b > b + a:
            return -1
        if a + b < b + a:
            return 1
        return 0

    result = "".join(sorted(map(str, nums), key=cmp_to_key(cmp)))
    if result and set(result) == {"0"}:
        return "0"
    return result


def sort_by_fields(records, *fields):
    return sorted(records, key=operator.itemgetter(*fields))


@singledispatch
def to_text(x):
    return repr(x)


@to_text.register(int)
def _(x):
    return f"int:{x}"


@to_text.register(float)
def _(x):
    return f"float:{x}"


@to_text.register(list)
def _(x):
    return "[" + ", ".join(to_text(i) for i in x) + "]"


def compose(*funcs):
    return reduce(lambda f, g: lambda x: f(g(x)), funcs, lambda x: x)


@cache
def fib(n):
    return n if n < 2 else fib(n - 1) + fib(n - 2)
