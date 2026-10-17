import numpy as np
import torch


def to_float_matrix(rows):
    """Список списков чисел -> тензор формы (n, d) dtype float32."""
    return torch.tensor(rows, dtype=torch.float32)


def standardize_columns(x):
    """Стандартизация по столбцам: (x - среднее) / std, где std - population (unbiased=False).

    Столбец с нулевым std должен стать нулевым (деление на 1, а не на 0). Без циклов; вход не менять.
    """
    mean = x.mean(dim=0, keepdim=True)
    std = x.std(dim=0, unbiased=False, keepdim=True)
    std = torch.where(std == 0, torch.ones_like(std), std)
    return (x - mean) / std


def pairwise_sq_dists(a, b):
    """a формы (n, d), b формы (m, d) -> матрица (n, m) квадратов евклидовых расстояний. Без циклов, через broadcasting."""
    diff = a[:, None, :] - b[None, :, :]
    return (diff ** 2).sum(dim=-1)


def share_with_numpy(t):
    """Вернуть numpy-массив, который ДЕЛИТ память с тензором t (изменение одного видно в другом)."""
    return t.numpy()


def copy_from_numpy(arr):
    """Вернуть float32-тензор с данными arr, НЕ делящий память с arr (даже если arr уже float32)."""
    return torch.tensor(arr, dtype=torch.float32)
