import numpy as np


def wrap_add(a, b, dtype):
    dt = np.dtype(dtype)
    return np.asarray(a, dtype=dt) + np.asarray(b, dtype=dt)


def widen_add(a, b):
    return np.asarray(a).astype(np.int64) + np.asarray(b).astype(np.int64)


def to_uint8(x):
    x = np.asarray(x, dtype=np.float64)
    return np.rint(np.clip(x, 0.0, 1.0) * 255.0).astype(np.uint8)


def promoted_dtype(a, b):
    return np.result_type(np.dtype(a), np.dtype(b)).name
