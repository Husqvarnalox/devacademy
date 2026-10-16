import numpy as np


def pairwise_sq_dists(A, B):
    A = np.asarray(A, dtype=np.float64)
    B = np.asarray(B, dtype=np.float64)
    diff = A[:, None, :] - B[None, :, :]
    return (diff * diff).sum(axis=-1)


def nearest_index(A, B):
    return np.argmin(pairwise_sq_dists(A, B), axis=1)
