import numpy as np


def row_view(a, i):
    return a[i]


def step_view(a, step):
    return a[::step]


def positives_copy(a):
    return a[a > 0]


def clip_negative(a):
    out = a.copy()
    out[out < 0] = 0
    return out


def reorder_columns(a, order):
    return a[:, order]
