import numpy as np
from numpy.lib.stride_tricks import sliding_window_view


def window_view(a, w):
    a = np.asarray(a)
    if w < 1 or w > a.shape[0]:
        raise ValueError("w должно быть в диапазоне 1..len(a)")
    return sliding_window_view(a, w)


def moving_average(a, w):
    return window_view(np.asarray(a, dtype=np.float64), w).mean(axis=1)
