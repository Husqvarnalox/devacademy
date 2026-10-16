import numpy as np


def broadcast_shape(s1, s2):
    s1, s2 = tuple(s1), tuple(s2)
    n = max(len(s1), len(s2))
    s1 = (1,) * (n - len(s1)) + s1
    s2 = (1,) * (n - len(s2)) + s2
    out = []
    for d1, d2 in zip(s1, s2):
        if d1 == d2 or d2 == 1:
            out.append(d1)
        elif d1 == 1:
            out.append(d2)
        else:
            raise ValueError(f"формы {s1} и {s2} несовместимы")
    return tuple(out)


def center_columns(a):
    a = np.asarray(a, dtype=np.float64)
    return a - a.mean(axis=0)


def row_normalize(a):
    a = np.asarray(a, dtype=np.float64)
    sums = a.sum(axis=1, keepdims=True)
    return np.divide(a, sums, out=np.zeros_like(a), where=sums != 0)


def outer_sum(x, y):
    x = np.asarray(x)
    y = np.asarray(y)
    return x[:, None] + y[None, :]
