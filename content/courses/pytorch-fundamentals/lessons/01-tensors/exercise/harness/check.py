import json
import os

import numpy as np
import torch

torch.set_num_threads(1)
NONCE = os.environ["DEVACADEMY_NONCE"]

import tensor_task as m


def report(case_id, passed, message=""):
    line = {"id": case_id, "passed": bool(passed), "message": message}
    print("##DEVACADEMY##", NONCE, json.dumps(line, ensure_ascii=False), flush=True)


def case(case_id, check):
    """check() возвращает None при успехе или строку с причиной провала; исключения - провал."""
    try:
        problem = check()
    except Exception as exc:
        report(case_id, False, f"исключение {type(exc).__name__}: {exc}")
    else:
        report(case_id, problem is None, problem or "")


def t_to_float_matrix():
    t = m.to_float_matrix([[1, 2, 3], [4, 5, 6]])
    if not isinstance(t, torch.Tensor) or t.dtype != torch.float32 or tuple(t.shape) != (2, 3):
        return f"ожидался float32-тензор формы (2, 3), получили {t!r}"
    if not torch.equal(t, torch.tensor([[1.0, 2.0, 3.0], [4.0, 5.0, 6.0]])):
        return f"значения не совпали: {t!r}"


def t_standardize_columns():
    g = torch.Generator().manual_seed(0)
    x = torch.randn(50, 3, generator=g) * torch.tensor([1.0, 5.0, 0.1]) + torch.tensor([0.0, 10.0, -3.0])
    before = x.clone()
    z = m.standardize_columns(x)
    if tuple(z.shape) != (50, 3):
        return f"форма результата должна быть (50, 3), получили {tuple(z.shape)}"
    if not torch.allclose(z.mean(dim=0), torch.zeros(3), atol=1e-5):
        return f"среднее по столбцам должно быть около 0, получили {z.mean(dim=0)!r}"
    if not torch.allclose(z.std(dim=0, unbiased=False), torch.ones(3), atol=1e-4):
        return f"std (unbiased=False) по столбцам должно быть около 1, получили {z.std(dim=0, unbiased=False)!r}"
    if not torch.equal(x, before):
        return "входной тензор менять нельзя"


def t_pairwise_values():
    a = torch.tensor([[0.0, 0.0], [1.0, 1.0]])
    b = torch.tensor([[3.0, 4.0], [0.0, 1.0], [1.0, 1.0]])
    d = m.pairwise_sq_dists(a, b)
    want = torch.tensor([[25.0, 1.0, 2.0], [13.0, 1.0, 0.0]])
    if tuple(d.shape) != (2, 3) or not torch.allclose(d, want):
        return f"ожидалось {want.tolist()}, получили {d.tolist()}"


def t_share_memory():
    t = torch.arange(6, dtype=torch.float32)
    arr = m.share_with_numpy(t)
    if not isinstance(arr, np.ndarray):
        return f"ожидался numpy.ndarray, получили {type(arr).__name__}"
    if not np.shares_memory(arr, t.numpy()):
        return "массив не делит память с тензором: используйте t.numpy()"
    t[0] = 100.0
    if arr[0] != 100.0:
        return "изменение тензора должно быть видно в массиве"
    arr[1] = -1.0
    if t[1].item() != -1.0:
        return "изменение массива должно быть видно в тензоре"


def t_copy_independent():
    for dtype in (np.float32, np.float64, np.int64):
        arr = np.arange(4, dtype=dtype)
        t = m.copy_from_numpy(arr)
        if t.dtype != torch.float32:
            return f"для входа {np.dtype(dtype).name} ожидался dtype float32, получили {t.dtype}"
        if np.shares_memory(arr, t.numpy()):
            return f"для входа {np.dtype(dtype).name} тензор делит память с массивом: нужна копия"
        arr[0] = 99
        if t[0].item() != 0.0:
            return "изменение исходного массива не должно влиять на тензор"


def t_constant_column():
    x = torch.tensor([[1.0, 5.0], [2.0, 5.0], [3.0, 5.0]])
    z = m.standardize_columns(x)
    if not torch.isfinite(z).all():
        return f"константный столбец дал нечисловые значения: {z!r}"
    if not torch.allclose(z[:, 1], torch.zeros(3)):
        return f"константный столбец должен стать нулевым, получили {z[:, 1]!r}"


def t_pairwise_vs_cdist():
    g = torch.Generator().manual_seed(1)
    a = torch.randn(7, 4, generator=g)
    b = torch.randn(5, 4, generator=g)
    d = m.pairwise_sq_dists(a, b)
    want = torch.cdist(a, b) ** 2
    if tuple(d.shape) != (7, 5) or not torch.allclose(d, want, atol=1e-4):
        return "результат должен совпасть с torch.cdist(a, b) ** 2"
    if (d < 0).any():
        return "квадраты расстояний не могут быть отрицательными"
    same = m.pairwise_sq_dists(a, a)
    if not torch.allclose(torch.diagonal(same), torch.zeros(7), atol=1e-6):
        return "расстояние от точки до себя должно быть 0"


case("to-float-matrix", t_to_float_matrix)
case("standardize-columns", t_standardize_columns)
case("pairwise-values", t_pairwise_values)
case("share-memory", t_share_memory)
case("copy-independent", t_copy_independent)
case("constant-column", t_constant_column)
case("pairwise-vs-cdist", t_pairwise_vs_cdist)
