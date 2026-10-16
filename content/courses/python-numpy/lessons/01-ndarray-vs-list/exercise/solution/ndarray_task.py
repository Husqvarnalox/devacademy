import numpy as np


def describe(a):
    return {
        "shape": tuple(a.shape),
        "dtype": a.dtype.name,
        "itemsize": int(a.itemsize),
        "nbytes": int(a.nbytes),
        "strides": tuple(a.strides),
        "c_contiguous": bool(a.flags["C_CONTIGUOUS"]),
    }


def scale_all(values, k):
    return np.asarray(values, dtype=np.float64) * k


def c_strides(shape, itemsize):
    strides = []
    step = int(itemsize)
    for dim in reversed(shape):
        strides.append(step)
        step *= int(dim)
    return tuple(reversed(strides))
